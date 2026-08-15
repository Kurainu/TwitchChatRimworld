using System;
using System.Collections.Generic;

namespace TwitchChat.Emotes
{
    public sealed class BttvProvider : IEmoteProvider
    {
        const string Cdn = "https://cdn.betterttv.net/emote/";

        // Swapped whole, not cleared and refilled: Load runs on a pool thread while the display thread resolves.
        volatile IReadOnlyDictionary<string, EmoteImage> EmoteNameCache = new Dictionary<string, EmoteImage>(StringComparer.Ordinal);

        public void Reset() => EmoteNameCache = new Dictionary<string, EmoteImage>(StringComparer.Ordinal);

        public void Load(string channelId)
        {
            var map = new Dictionary<string, EmoteImage>(StringComparer.Ordinal);
            Add(map, EmoteHttp.Fetch("BTTV", "https://api.betterttv.net/3/cached/emotes/global"));
            if (!string.IsNullOrEmpty(channelId))
            {
                object channel = EmoteHttp.Fetch("BTTV", "https://api.betterttv.net/3/cached/users/twitch/" + channelId);
                // channel after globals so it wins the clash, and its own emotes after the ones shared into it
                Add(map, EmoteHttp.Field(channel, "sharedEmotes"));
                Add(map, EmoteHttp.Field(channel, "channelEmotes"));
            }

            EmoteNameCache = map;

            EmoteLog.Dev($"[Twitch Chat] loaded {EmoteNameCache.Count} BTTV emotes");
        }

        public bool TryResolve(string text, out EmoteImage image) => EmoteNameCache.TryGetValue(text, out image);

        // globals are a bare array; a channel wraps its own and shared emotes, so both entry points pass a list here
        static void Add(Dictionary<string, EmoteImage> map, object emotes)
        {
            foreach (object node in EmoteHttp.Arr(emotes))
            {
                string id = EmoteHttp.Id(node, "id");
                string code = EmoteHttp.Str(node, "code");
                if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(code)) continue;

                map[code] = new EmoteImage(id, Cdn + id + "/2x");
            }
        }
    }
}
