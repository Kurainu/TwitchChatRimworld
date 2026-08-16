using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using TwitchChat.Emotes;
using TwitchChat.Settings;
using TwitchChat.Naming;
using UnityEngine;
using Verse;
using ModConfig = TwitchChat.Settings.Settings;

namespace TwitchChat.Chat;

internal static class TwitchClient
{
	private static readonly TwitchChatSocket client = new TwitchChatSocket();
	private static readonly EmotePipeline emotes = new EmotePipeline();
	private static volatile string emoteRoomId;
	private static string emoteLoadError;
	public static string LastMessage = "";
	public static string CurrentChannel => client.Channel;

	// Known service bots, matched by login; skipped by default (ignoreServiceBots).
	private static readonly HashSet<string> ServiceBots = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
	{
		"nightbot", "streamelements", "streamlabs", "moobot", "wizebot", "fossabot",
		"ankhbot", "phantombot", "sery_bot", "commanderroot", "soundalerts", "kofistreambot",
		"tangiabot", "pretzelrocks", "creatisbot", "PokemonCommunityGame",
	};

	private static bool keepConnected;
	private static float lastConnectTime;
	private static float connectRetryDelay = 5f;
	private const float ConnectTimeoutSec = 20f;
	private const double StaleTimeoutSec = 360.0;
	private const double KeepAliveIdleSec = 120.0;
	private const float KeepAliveMinIntervalSec = 30f;
	private static float lastKeepAliveTime;

	private static readonly object stateLock = new object();

	private static ModConfig Config => LoadedModManager.GetMod<TwitchChatSettingsMod>().GetSettings<ModConfig>();

	public static bool IsConnected() => client.Connected && !client.IsStale(StaleTimeoutSec);

	// connection state for the settings UI
	public enum ConnectionState
	{
		NoChannel,
		Connecting,
		Connected,
		Disconnected,
	}

	public static ConnectionState GetState()
	{
		if (IsConnected()) return ConnectionState.Connected;
		if (NormalizeChannel(Config.twitchChannel).Length == 0) return ConnectionState.NoChannel;
		if (keepConnected) return ConnectionState.Connecting;
		return ConnectionState.Disconnected;
	}

	// Called ~1x/second from ChatNamer.Tick: (re)connect with backoff, then drain queued chat.
	public static void CheckState()
	{
		string err = client.TakeError();
		if (err != null)
		{
			Log.Warning("[Twitch Chat] connection error: " + err);
		}

		string emoteErr = Interlocked.Exchange(ref emoteLoadError, null);
		if (emoteErr != null)
		{
			Log.Warning("[Twitch Chat] emote load failed: " + emoteErr);
		}
		EmoteLog.Flush();

		bool healthy;
		lock (stateLock)
		{
			healthy = IsConnected();
			if (healthy)
			{
				connectRetryDelay = 5f; // reset backoff once we're connected
			}
			else if (!keepConnected)
			{
				// idle: connect only on the button, or reconnect a dropped session that was already active
			}
			else if (client.Alive && Time.time - lastConnectTime < ConnectTimeoutSec)
			{
				// attempt in flight; wait
			}
			else if (lastConnectTime + connectRetryDelay < Time.time)
			{
				lastConnectTime = Time.time;
				connectRetryDelay = Mathf.Min(connectRetryDelay + 5f, 120f);
				Connect();
			}
		}

		// Outside the lock: draining and the keepalive Send must not block teardown.
		if (healthy)
		{
			// ROOMSTATE lands on join, so the sets are usually loading before the first message.
			EnsureEmotes(client.RoomId);
			while (client.TryReadLine(out ChatLine line))
			{
				HandleMessage(line);
			}
			MaybeKeepAlive();
		}
	}

	// fetch third-party emote sets once we learn the room-id; Load blocks on the network so it runs off-thread
	private static void EnsureEmotes(string roomId)
	{
		if (string.IsNullOrEmpty(roomId) || roomId == emoteRoomId) return;

		emoteRoomId = roomId;
		ThreadPool.QueueUserWorkItem(_ =>
		{
			// Broad on purpose: an exception escaping a pool thread takes the process down with it.
			try { emotes.Load(roomId); }
			catch (Exception ex) { emoteLoadError = ex.Message; }
		});
	}

	private static void Connect()
	{
		string channel = NormalizeChannel(Config.twitchChannel);
		if (channel.Length == 0)
		{
			keepConnected = false; // nothing to connect to; stop trying
			return;
		}
		ChatterPool.Clear(); // fresh connection: drop the previous channel's chatters
		emotes.Reset();      // and its emote sets; EnsureEmotes refetches once the new channel's room-id arrives
		LastMessage = "";
		emoteRoomId = null;
		client.Connect(channel);
	}

	private static void MaybeKeepAlive()
	{
		if (client.IdleSeconds < KeepAliveIdleSec || Time.time - lastKeepAliveTime < KeepAliveMinIntervalSec) return;
		lastKeepAliveTime = Time.time;
		// Off-thread: Ping's send is synchronous; don't freeze the main thread on a half-open socket.
		ThreadPool.QueueUserWorkItem(_ => client.Ping());
	}

	private static void ResetConnectionState()
	{
		lock (stateLock)
		{
			client.Close();
			LastMessage = "";
			emoteRoomId = null;
			connectRetryDelay = 5f;
			lastConnectTime = 0f;
		}
	}

	public static void Reconnect()
	{
		keepConnected = true;
		ResetConnectionState();
		CheckState();
	}

	// one-shot on game start: bring the socket up if a channel is set, so the player doesn't have to hit the button
	public static void AutoConnect()
	{
		if (IsConnected() || NormalizeChannel(Config.twitchChannel).Length == 0) return;
		Reconnect();
	}

	// Tear down on leaving/quitting; stays disconnected until the user connects again.
	public static void Shutdown()
	{
		keepConnected = false;
		ResetConnectionState();
	}

	private static void HandleMessage(ChatLine line)
	{
		if (Prefs.DevMode)
        {
            Log.Message($"[Twitch Chat] recv login='{line.Login}' display='{line.Display}' " + string.Join(" | ", emotes.Parse(line.Message, line.Emotes).Select(seg => !seg.IsEmote ? $"text \"{seg.Text}\"" : seg.Image.Url != null ? $"emote {seg.Text} {seg.Image.Url}" : $"emote {seg.Text} unresolved")));
        }

        var settings = Config;

		// skip known service bots (by login)
		if (settings.ignoreServiceBots && line.Login != null && ServiceBots.Contains(line.Login)) return;

		// blacklist matches login or display name, case-insensitive
		if (IsBlacklisted(line.Login, settings.blacklist)
			|| (line.Display != line.Login && IsBlacklisted(line.Display, settings.blacklist)))
		{
			return;
		}

		LastMessage = $"{line.Display}: {line.Message}";

		ChatNamer.OnMessage(line.Login, line.Display, emotes.Parse(line.Message, line.Emotes));
	}

	private static bool IsBlacklisted(string user, List<string> blacklist)
	{
		if (string.IsNullOrEmpty(user) || blacklist == null) return false;
		foreach (string entry in blacklist)
		{
			if (string.Equals(entry, user, StringComparison.OrdinalIgnoreCase)) return true;
		}
		return false;
	}

	// normalize channel input to the lowercase login (or ""): a bare name, "#name", "@name", or a pasted twitch.tv/name URL
	private static string NormalizeChannel(string raw)
	{
		if (string.IsNullOrWhiteSpace(raw)) return "";
		string s = raw.Trim().ToLowerInvariant();
		int url = s.LastIndexOf("twitch.tv/", StringComparison.Ordinal);
		if (url >= 0)
		{
			s = s.Substring(url + "twitch.tv/".Length);
		}
		s = s.TrimStart('#', '@');
		return LoginChars.Match(s).Value;
	}

	private static readonly Regex LoginChars = new Regex("^[a-z0-9_]*");
}
