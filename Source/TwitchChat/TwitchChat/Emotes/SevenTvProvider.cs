using System;
using System.Collections.Generic;

namespace TwitchChat.Emotes
{
    public sealed class SevenTvProvider : IEmoteProvider
    {
        static readonly string[] Formats = { "png", "webp" };

        volatile IReadOnlyDictionary<string, EmoteImage> EmoteNameCache = new Dictionary<string, EmoteImage>(StringComparer.Ordinal);

        public void Reset() => EmoteNameCache = new Dictionary<string, EmoteImage>(StringComparer.Ordinal);

        public void Load(string channelId)
        {
            var map = new Dictionary<string, EmoteImage>(StringComparer.Ordinal);
            AddSet(map, EmoteHttp.Fetch("7TV", "https://7tv.io/v3/emote-sets/global"));
            if (!string.IsNullOrEmpty(channelId))
            {
                object user = EmoteHttp.Fetch("7TV", "https://7tv.io/v3/users/twitch/" + channelId);
                AddSet(map, EmoteHttp.Field(user, "emote_set"));
            }

            EmoteNameCache = map;

            EmoteLog.Dev($"[Twitch Chat] loaded {EmoteNameCache.Count} 7TV emotes");
        }

        public bool TryResolve(string text, out EmoteImage image) => EmoteNameCache.TryGetValue(text, out image);

        static void AddSet(Dictionary<string, EmoteImage> map, object set)
        {
            foreach (object node in EmoteHttp.Arr(EmoteHttp.Field(set, "emotes")))
            {
                object host = EmoteHttp.Field(EmoteHttp.Field(node, "data"), "host");
                string name = EmoteHttp.Str(node, "name");
                string url = EmoteHttp.Str(host, "url");
                if (name == null || string.IsNullOrEmpty(url)) continue;

                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (object file in EmoteHttp.Arr(EmoteHttp.Field(host, "files")))
                {
                    string fileName = EmoteHttp.Str(file, "name");
                    if (fileName != null)
                    {
                        names.Add(fileName);
                    }
                }

                string picked = null;
                foreach (string ext in Formats)
                {
                    if (names.Contains("2x." + ext))
                    {
                        picked = "2x." + ext;
                        break;
                    }
                }

                if (picked != null)
                {
                    map[name] = new EmoteImage(EmoteHttp.Id(node, "id"), "https:" + url + "/" + picked);
                }
            }
        }
    }
}
