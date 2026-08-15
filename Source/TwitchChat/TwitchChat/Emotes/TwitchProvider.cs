using System;
using System.Collections.Generic;
using TwitchChat.Chat;

namespace TwitchChat.Emotes
{
    // Twitch native emotes are per message, not per channel; the IRC tag names spans by code-point index
    public sealed class TwitchProvider : IEmoteProvider
    {
        const string Cdn = "https://static-cdn.jtvnw.net/emoticons/v2/";

        readonly Dictionary<string, EmoteImage> EmoteNameCache = new Dictionary<string, EmoteImage>(StringComparer.Ordinal);

        //No Channel fetch needed its all in the Message that comes Through the irc Websocket
        public void Load(string channelId) { }

        public void Parse(string message, IReadOnlyList<Emote> emotes)
        {
            EmoteNameCache.Clear();
            if (string.IsNullOrEmpty(message) || emotes == null) return;

            int[] starts = CodePointStarts(message);
            foreach (Emote e in emotes)
            {
                if (e.Start < 0 || e.End < e.Start || e.End >= starts.Length) continue;
                string word = Slice(message, starts, e.Start, e.End);
                EmoteNameCache[word] = new EmoteImage(e.Id, Cdn + e.Id + "/default/dark/2.0");
            }
        }

        public bool TryResolve(string text, out EmoteImage image) => EmoteNameCache.TryGetValue(text, out image);

        static int[] CodePointStarts(string s)
        {
            var starts = new List<int>(s.Length);
            for (int i = 0; i < s.Length; )
            {
                starts.Add(i);
                bool pair = char.IsHighSurrogate(s[i]) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1]);
                i += pair ? 2 : 1;
            }
            return starts.ToArray();
        }

        static string Slice(string s, int[] starts, int cpStart, int cpEnd)
        {
            int a = starts[cpStart];
            int b = cpEnd + 1 < starts.Length ? starts[cpEnd + 1] : s.Length;
            return s.Substring(a, b - a);
        }
    }
}
