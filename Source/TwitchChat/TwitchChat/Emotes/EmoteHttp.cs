using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using MiniJSON;

namespace TwitchChat.Emotes
{
    static class EmoteHttp
    {
        // one client for every provider (a new one per set leaks sockets on reconnect); caps bound a slow or oversized response instead of hanging or buffering to OOM
        static readonly HttpClient Http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15),
            MaxResponseContentBufferSize = 16L * 1024 * 1024,
        };

        // a service 404s for a channel with no account (normal), so a failed fetch returns null and the caller carries on
        public static object Fetch(string service, string url)
        {
            try
            {
                return Json.Deserialize(Http.GetStringAsync(url).GetAwaiter().GetResult());
            }
            catch (HttpRequestException ex)
            {
                EmoteLog.Dev($"[Twitch Chat] {service} fetch failed ({url}): {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                // timeout is TaskCanceledException, torn read is IOException; neither is worth killing the loading thread
                EmoteLog.Dev($"[Twitch Chat] {service} fetch failed ({url}): {ex.Message}");
                return null;
            }
        }


        static readonly List<object> NoItems = new List<object>();

        public static List<object> Arr(object node) => node as List<object> ?? NoItems;

        public static Dictionary<string, object> Obj(object node) => node as Dictionary<string, object>;

        public static object Field(object node, string key)
        {
            var map = node as Dictionary<string, object>;
            return map != null && map.TryGetValue(key, out object v) ? v : null;
        }

        public static string Str(object node, string key) => Field(node, key) as string;

        // Whole numbers come back as long, so an id arrives as either that or a string depending on the API.
        public static string Id(object node, string key)
        {
            object v = Field(node, key);
            if (v is string s) return s;
            if (v is long l) return l.ToString(CultureInfo.InvariantCulture);
            return null;
        }
    }
}
