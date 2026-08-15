using System.Text;

namespace TwitchChat.Emotes
{
    // Unicode emoji from the baked table; MessageSegments detects them, here we just map a cluster to its pajbot image
    public sealed class EmojiProvider : IEmoteProvider
    {
        const string Cdn = "https://pajbot.com/static/emoji-v2/img/twitter/64/";

        public void Load(string channelId) { }

        public bool TryResolve(string text, out EmoteImage image)
        {
            string hex = Hex(text);
            if (Emojis.Codes.Contains(hex))
            {
                image = new EmoteImage(hex, Cdn + hex + ".png");
                return true;
            }
            image = default(EmoteImage);
            return false;
        }

        static string Hex(string text)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < text.Length; )
            {
                char c = text[i];
                int cp;
                if (char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                {
                    cp = char.ConvertToUtf32(c, text[i + 1]);
                    i += 2;
                }
                else
                {
                    cp = c;
                    i += 1;
                }
                if (sb.Length > 0)
                {
                    sb.Append('-');
                }

                sb.Append(cp.ToString("x4"));
            }
            return sb.ToString();
        }
    }
}
