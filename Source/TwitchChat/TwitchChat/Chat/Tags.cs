using System;
using System.Collections.Generic;
using System.Text;

namespace TwitchChat.Chat;

// One emote occurrence in a message. Start/End are inclusive, code-point indices.
public readonly struct Emote
{
	public readonly string Id;
	public readonly int Start, End;
	public Emote(string id, int start, int end) { Id = id; Start = start; End = end; }
}

// the "@..." IRCv3 tag blob parsed once into a dict; typed accessors for the tags we use, anything else via tags["key"]
public sealed class Tags
{
	readonly Dictionary<string, string> raw;

	public Tags(string blob) => raw = Parse(blob);

	public string DisplayName => Get("display-name");
	public string Color => Get("color");
	public bool IsMod => Get("mod") == "1";
	public bool IsVip => Get("vip") == "1";
	public bool IsSub => Get("subscriber") == "1";
	public int Bits => int.TryParse(Get("bits"), out int b) ? b : 0;
	public List<Emote> Emotes => ParseEmotes(Get("emotes"));

	public string this[string key] => Get(key);

	string Get(string key) => raw.TryGetValue(key, out string v) ? v : null;

	static Dictionary<string, string> Parse(string blob)
	{
		var tags = new Dictionary<string, string>(StringComparer.Ordinal);
		if (string.IsNullOrEmpty(blob)) return tags;
		if (blob[0] == '@')
		{
			blob = blob.Substring(1);
		}

		foreach (string pair in blob.Split(';'))
		{
			if (pair.Length == 0) continue;
			int eq = pair.IndexOf('=');
			if (eq < 0)
			{
				tags[pair] = "";
			}
			else
			{
				tags[pair.Substring(0, eq)] = Unescape(pair.Substring(eq + 1));
			}
		}
		return tags;
	}

	// "25:31-35,40-44/1902:6-10" -> [(25,31,35), (25,40,44), (1902,6,10)]
	static List<Emote> ParseEmotes(string s)
	{
		var list = new List<Emote>();
		if (string.IsNullOrEmpty(s)) return list;
		foreach (string chunk in s.Split('/'))
		{
			int colon = chunk.IndexOf(':');
			if (colon < 0) continue;
			string id = chunk.Substring(0, colon);
			foreach (string span in chunk.Substring(colon + 1).Split(','))
			{
				int dash = span.IndexOf('-');
				if (dash < 0) continue;
				if (int.TryParse(span.Substring(0, dash), out int a) &&
					int.TryParse(span.Substring(dash + 1), out int b))
				{
					list.Add(new Emote(id, a, b));
				}
			}
		}
		return list;
	}

	// IRCv3 unescape: \s=space, \:=; \\=\ \r \n; a lone trailing backslash is dropped.
	static string Unescape(string v)
	{
		if (v.Length == 0 || v.IndexOf('\\') < 0) return v;
		var sb = new StringBuilder(v.Length);
		for (int i = 0; i < v.Length; i++)
		{
			char c = v[i];
			if (c != '\\') { sb.Append(c); continue; }
			if (i + 1 == v.Length) break;
			switch (v[++i])
			{
			case 's': sb.Append(' '); break;
			case ':': sb.Append(';'); break;
			case 'r': sb.Append('\r'); break;
			case 'n': sb.Append('\n'); break;
			case '\\': sb.Append('\\'); break;
			default: sb.Append(v[i]); break;
			}
		}
		return sb.ToString();
	}
}
