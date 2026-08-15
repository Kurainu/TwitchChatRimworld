using System;
using System.Collections.Generic;

namespace TwitchChat.Emotes
{
    public sealed class FfzProvider : IEmoteProvider
    {
        // largest scale first; FFZ emotes come out bigger than the others' 2x, switch to "2" if that shows
        static readonly string[] Scales = { "4", "2", "1" };

        // Swapped whole, not cleared and refilled: Load runs on a pool thread while the display thread resolves.
        volatile IReadOnlyDictionary<string, EmoteImage> EmoteNameCache = new Dictionary<string, EmoteImage>(StringComparer.Ordinal);

        public void Reset() => EmoteNameCache = new Dictionary<string, EmoteImage>(StringComparer.Ordinal);

        public void Load(string channelId)
        {
            var map = new Dictionary<string, EmoteImage>(StringComparer.Ordinal);

            object global = EmoteHttp.Fetch("FFZ", "https://api.frankerfacez.com/v1/set/global");
            object globalSets = EmoteHttp.Field(global, "sets");
            // The response carries sets that are not in default_sets; those are not served globally.
            foreach (object id in EmoteHttp.Arr(EmoteHttp.Field(global, "default_sets")))
            {
                Add(map, EmoteHttp.Field(globalSets, Key(id)));
            }

            if (!string.IsNullOrEmpty(channelId))
            {
                object room = EmoteHttp.Fetch("FFZ", "https://api.frankerfacez.com/v1/room/id/" + channelId);
                Dictionary<string, object> sets = EmoteHttp.Obj(EmoteHttp.Field(room, "sets"));
                if (sets != null)
                {
                    // After the globals, so a channel reusing a global name wins the clash.
                    foreach (object set in sets.Values)
                    {
                        Add(map, set);
                    }
                }
            }

            EmoteNameCache = map;

            EmoteLog.Dev($"[Twitch Chat] loaded {EmoteNameCache.Count} FFZ emotes");
        }

        public bool TryResolve(string text, out EmoteImage image) => EmoteNameCache.TryGetValue(text, out image);

        // default_sets holds numbers while sets is keyed by those same ids as strings.
        static string Key(object id) => id is long l ? l.ToString() : id as string;

        static void Add(Dictionary<string, EmoteImage> map, object set)
        {
            foreach (object node in EmoteHttp.Arr(EmoteHttp.Field(set, "emoticons")))
            {
                string name = EmoteHttp.Str(node, "name");
                object urls = EmoteHttp.Field(node, "urls");
                if (string.IsNullOrEmpty(name) || urls == null) continue;

                string url = PickUrl(urls);
                if (url != null)
                {
                    map[name] = new EmoteImage(EmoteHttp.Id(node, "id"), url);
                }
            }
        }

        // FFZ hands back absolute https URLs keyed by scale, unlike the protocol-relative ones it used to send.
        static string PickUrl(object urls)
        {
            foreach (string scale in Scales)
            {
                string url = EmoteHttp.Str(urls, scale);
                if (!string.IsNullOrEmpty(url)) return url;
            }
            return null;
        }
    }
}
