using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using Verse;

namespace TwitchChat.Chat;

// one parsed chat line, read thread to game thread; Emotes is null when the line had no emote tag (the common case)
public readonly struct ChatLine
{
	public readonly string Login, Display, Message, Raw;
	public readonly Emote[] Emotes;
	public ChatLine(string login, string display, string message, string raw, Emote[] emotes)
	{
		Login = login;
		Display = display;
		Message = message;
		Raw = raw;
		Emotes = emotes;
	}
}

// anonymous read-only Twitch reader: a background thread enqueues PRIVMSGs, the game thread drains them; minimal IRC (CAP/NICK/JOIN/PING/RECONNECT)
public sealed class TwitchChatSocket : IDisposable
{
	private const string Endpoint = "wss://irc-ws.chat.twitch.tv:443";
	// Cap so the queue can't grow unbounded while the game thread isn't draining (paused / menu).
	private const int MaxQueued = 500;
	// A single IRC line sits well under this; a peer streaming bytes with no newline can't grow pending past it.
	private const int MaxPending = 64 * 1024;

	private readonly ConcurrentQueue<ChatLine> inbox = new ConcurrentQueue<ChatLine>();
	private readonly object sendLock = new object();
	private readonly string nick = "justinfan" + Rand.Range(10000, 99999);

	private volatile ClientWebSocket socket;
	private volatile CancellationTokenSource cts;
	private string channel = "";
	private volatile string joined = "";
	private volatile string roomId = "";
	private volatile string error;
	private string pending = "";
	private DateTime lastReceivedUtc = DateTime.UtcNow;

	// True while a socket exists (connecting or connected); lets the poller tell "trying" from "dead".
	public bool Alive => socket != null;
	public bool Connected => socket != null && socket.State == WebSocketState.Open && joined.Length > 0;
	public string Channel => joined;
	// The channel's numeric Twitch id, from ROOMSTATE on join. Empty until then.
	public string RoomId => roomId;
	public double IdleSeconds => (DateTime.UtcNow - lastReceivedUtc).TotalSeconds;
	public bool IsStale(double maxIdleSeconds) => Connected && IdleSeconds > maxIdleSeconds;

	// Drained by the game thread each tick.
	public bool TryReadLine(out ChatLine line) => inbox.TryDequeue(out line);

	// Last error, cleared on read; logged by the game thread.
	public string TakeError()
	{
		string e = error;
		error = null;
		return e;
	}

	public void Connect(string channelName)
	{
		Close();
		while (inbox.TryDequeue(out _)) { } // drop any stale lines from the previous connection
		channel = channelName;
		joined = "";
		roomId = "";
		cts = new CancellationTokenSource();
		socket = new ClientWebSocket();
		new Thread(Run) { IsBackground = true }.Start();
	}

	public void Close()
	{
		// Cancel and abort to unwind the read loop; Run's finally disposes the socket and cts and nulls the field.
		try { cts?.Cancel(); } catch { }
		try { socket?.Abort(); } catch { }
	}

	public void Ping() => Send("PING :tmi.twitch.tv");

	public void Dispose() => Close();

	private void Run()
	{
		// this run owns the socket and cts Connect made, so finally disposes them; without it every reconnect leaked one
		CancellationTokenSource myCts = cts;
		ClientWebSocket ws = socket;
		try
		{
			ws.ConnectAsync(new Uri(Endpoint), myCts.Token).GetAwaiter().GetResult();
			pending = "";
			lastReceivedUtc = DateTime.UtcNow;
			// commands cap delivers ROOMSTATE (the room-id); Twitch requires it alongside tags
			Send("CAP REQ :twitch.tv/tags twitch.tv/commands");
			Send("NICK " + nick);

			byte[] buffer = new byte[4096];
			while (ws.State == WebSocketState.Open)
			{
				WebSocketReceiveResult result = ws.ReceiveAsync(new ArraySegment<byte>(buffer), myCts.Token).GetAwaiter().GetResult();
				if (result.MessageType == WebSocketMessageType.Close) break;
				lastReceivedUtc = DateTime.UtcNow;
				pending += Encoding.UTF8.GetString(buffer, 0, result.Count);
				DrainLines();
			}
		}
		catch (Exception ex)
		{
			// A cancellation we triggered ourselves (Close) is expected teardown, not an error.
			if (myCts == null || !myCts.IsCancellationRequested)
			{
				error = ex.Message;
			}
		}
		finally
		{
			ws.Dispose();
			myCts.Dispose();
			// drop the socket if it's still ours (a reconnect may have swapped it) so the poller reconnects
			if (cts == myCts)
			{
				socket = null;
			}
		}
	}

	private void DrainLines()
	{
		int idx;
		while ((idx = pending.IndexOf('\n')) >= 0)
		{
			string line = pending.Substring(0, idx).TrimEnd('\r');
			pending = pending.Substring(idx + 1);
			if (line.Length > 0)
			{
				Handle(line);
			}
		}
		// no newline in an oversized buffer means a broken or hostile peer; drop the partial rather than grow it
		if (pending.Length > MaxPending)
		{
			pending = "";
		}
	}

	private void Handle(string data)
	{
		string raw = data;

		// optional "@tags " prefix
		Tags tags = null;
		if (data[0] == '@')
		{
			string[] head = data.Split(new[] { ' ' }, 2);
			if (head.Length < 2) return;
			tags = new Tags(head[0]);
			data = head[1];
		}

		// a server PING has no prefix: "PING :tmi.twitch.tv"
		if (data.StartsWith("PING", StringComparison.Ordinal))
		{
			Send("PONG :tmi.twitch.tv");
			return;
		}

		if (data.Length == 0 || data[0] != ':') return;
		string[] parts = data.Split(new[] { ' ' }, 3);
		if (parts.Length < 2) return;
		string command = parts[1];
		string rest = parts.Length > 2 ? parts[2] : "";

		switch (command)
		{
		case "001":
			// registered; join our channel
			Send("JOIN #" + channel);
			break;

		case "JOIN":
			// Twitch echoes our own JOIN once we're in
			joined = channel;
			break;

		// Sent on join once we hold the commands cap; carries the channel's numeric id.
		case "ROOMSTATE":
		{
			string rid = tags?["room-id"];
			if (!string.IsNullOrEmpty(rid))
			{
				roomId = rid;
			}
			break;
		}

		case "RECONNECT":
			// server restart; drop the socket so the poller reconnects
			Close();
			break;

		case "PRIVMSG":
		{
			// rest = "#channel :message text"; split the trailing message off the first " :"
			string[] body = rest.Split(new[] { " :" }, 2, StringSplitOptions.None);
			string message = body.Length > 1 ? body[1] : "";
			string login = NickOf(parts[0]);
			string display = tags != null && !string.IsNullOrEmpty(tags.DisplayName) ? tags.DisplayName : login;
			List<Emote> spans = tags?.Emotes;
			Emote[] emotes = spans != null && spans.Count > 0 ? spans.ToArray() : null;
			inbox.Enqueue(new ChatLine(login, display, message, raw, emotes));
			while (inbox.Count > MaxQueued && inbox.TryDequeue(out _)) { } // drop oldest if the drain fell behind
			break;
		}
		}
	}

	// ":nick!user@host" -> "nick". A server prefix with no '!' (e.g. tmi.twitch.tv) comes back whole.
	private static string NickOf(string prefix)
	{
		int start = prefix.Length > 0 && prefix[0] == ':' ? 1 : 0;
		int bang = prefix.IndexOf('!', start);
		return bang > start ? prefix.Substring(start, bang - start) : prefix.Substring(start);
	}

	private void Send(string message)
	{
		ClientWebSocket s = socket;
		CancellationTokenSource baseCts = cts;
		if (s == null || baseCts == null) return;
		byte[] bytes = Encoding.UTF8.GetBytes(message + "\r\n");
		try
		{
			// ClientWebSocket forbids overlapping sends (read-thread PONG vs game-thread Ping); cap so a half-open socket can't hang the caller
			lock (sendLock)
			{
				using (CancellationTokenSource sendCts = CancellationTokenSource.CreateLinkedTokenSource(baseCts.Token))
				{
					sendCts.CancelAfter(5000);
					s.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, endOfMessage: true, sendCts.Token)
						.GetAwaiter().GetResult();
				}
			}
		}
		catch
		{
			// dead socket; drop it (if still ours) so the poller reconnects
			try { s.Abort(); } catch { }
			if (socket == s)
			{
				socket = null;
			}
		}
	}
}
