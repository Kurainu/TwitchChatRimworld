using System.Collections.Generic;
using System.Text;

namespace TwitchChat.Emotes
{
    // One piece of a parsed message: a run of plain text, or a single emote (emoji).
    public readonly struct MessageSegment
    {
        public readonly string Text;
        public readonly bool IsEmote;
        public MessageSegment(string text, bool isEmote) { Text = text; IsEmote = isEmote; }
    }

    public static class MessageSegments
    {
        // split into text runs and emote segments; an emoji plus its trailing glue is one emote, every ZWJ chain fuses
        public static List<MessageSegment> Of(string s)
        {
            var segments = new List<MessageSegment>();
            var text = new StringBuilder();
            int i = 0;
            while (i < s.Length)
            {
                int start = i;
                int baseCp = NextSegment(s, ref i);

                // a country flag is two Regional Indicator letters in a row
                if (baseCp >= 0x1F1E6 && baseCp <= 0x1F1FF)
                {
                    if (i < s.Length)
                    {
                        int save = i;
                        int partner = NextSegment(s, ref i);
                        if (partner < 0x1F1E6 || partner > 0x1F1FF)
                        {
                            i = save;
                        }
                    }
                    Add(segments, text, s.Substring(start, i - start));
                    continue;
                }

                // pull in this emoji's trailing glue code points
                while (i < s.Length)
                {
                    int save = i;
                    int cp = NextSegment(s, ref i);
                    if (cp == 0x200D) // ZWJ
                    {
                        if (i < s.Length)
                        {
                            NextSegment(s, ref i);   // the joined emoji
                        }

                        continue;
                    }

                    bool glue = cp == 0xFE0F || cp == 0xFE0E || cp == 0x20E3  // selectors, keycap
                             || (cp >= 0x1F3FB && cp <= 0x1F3FF)              // skin tones
                             || (cp >= 0xE0020 && cp <= 0xE007F);             // flag tag 
                    if (!glue) { i = save; break; }
                }
                Add(segments, text, s.Substring(start, i - start));
            }
            Flush(segments, text);
            return segments;
        }

        // an emote becomes its own segment after flushing pending text; a plain char accumulates into the run
        static void Add(List<MessageSegment> segments, StringBuilder text, string cluster)
        {
            if (cluster.Length > 1 || IsBmpEmoji(cluster[0]))
            {
                Flush(segments, text);
                segments.Add(new MessageSegment(cluster, true));
            }
            else
            {
                text.Append(cluster);
            }
        }

        static void Flush(List<MessageSegment> segments, StringBuilder text)
        {
            if (text.Length == 0) return;
            segments.Add(new MessageSegment(text.ToString(), false));
            text.Clear();
        }

        static int NextSegment(string s, ref int i)
        {
            char hi = s[i];
            if (char.IsHighSurrogate(hi) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1]))
            {
                int cp = char.ConvertToUtf32(hi, s[i + 1]);
                i += 2;
                return cp;
            }
            i += 1;
            return hi;
        }

        // BMP code points that render as emoji alone (Emoji_Presentation, Unicode 17.0), as sorted [lo,hi] pairs
        static readonly int[] BmpEmoji =
        {
            0x231A, 0x231B, 0x23E9, 0x23EC, 0x23F0, 0x23F0, 0x23F3, 0x23F3,
            0x25FD, 0x25FE, 0x2614, 0x2615, 0x2648, 0x2653, 0x267F, 0x267F,
            0x2693, 0x2693, 0x26A1, 0x26A1, 0x26AA, 0x26AB, 0x26BD, 0x26BE,
            0x26C4, 0x26C5, 0x26CE, 0x26CE, 0x26D4, 0x26D4, 0x26EA, 0x26EA,
            0x26F2, 0x26F3, 0x26F5, 0x26F5, 0x26FA, 0x26FA, 0x26FD, 0x26FD,
            0x2705, 0x2705, 0x270A, 0x270B, 0x2728, 0x2728, 0x274C, 0x274C,
            0x274E, 0x274E, 0x2753, 0x2755, 0x2757, 0x2757, 0x2795, 0x2797,
            0x27B0, 0x27B0, 0x27BF, 0x27BF, 0x2B1B, 0x2B1C, 0x2B50, 0x2B50,
            0x2B55, 0x2B55,
        };

        static bool IsBmpEmoji(int cp)
        {
            for (int i = 0; i < BmpEmoji.Length; i += 2)
            {
                if (cp < BmpEmoji[i])
                {
                    return false;       // sorted, so nothing further can match
                }
                if (cp <= BmpEmoji[i + 1]) return true;
            }
            return false;
        }
    }
}
