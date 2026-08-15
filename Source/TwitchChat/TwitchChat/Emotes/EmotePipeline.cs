using System.Collections.Generic;
using System.Text;
using TwitchChat.Chat;
using TwitchChat.Settings;
using Verse;
using ModConfig = TwitchChat.Settings.Settings;

namespace TwitchChat.Emotes
{
    // split a message and resolve each emote: Twitch tag first, then emoji, then the third-party word sets, each gated by its setting
    public sealed class EmotePipeline
    {
        // Rebound from each line's tag; nothing to fetch per channel.
        readonly TwitchProvider twitch = new TwitchProvider();
        readonly EmojiProvider emoji = new EmojiProvider();
        readonly SevenTvProvider sevenTv = new SevenTvProvider();
        readonly BttvProvider bttv = new BttvProvider();
        readonly FfzProvider ffz = new FfzProvider();

        static ModConfig Config => LoadedModManager.GetMod<TwitchChatSettingsMod>().GetSettings<ModConfig>();

        // Snapshot of the toggles for the current Parse; game-thread only, so a field is safe.
        ModConfig cfg;

        public void Load(string channelId)
        {
            sevenTv.Load(channelId);
            bttv.Load(channelId);
            ffz.Load(channelId);
        }

        // drop the channel emote sets on a channel switch; the next Load refetches for the new channel
        public void Reset()
        {
            sevenTv.Reset();
            bttv.Reset();
            ffz.Reset();
        }

        public List<ChatSegment> Parse(string message, IReadOnlyList<Emote> twitchEmotes)
        {
            cfg = Config;
            twitch.Parse(message, twitchEmotes);

            var segments = new List<ChatSegment>();
            foreach (MessageSegment seg in MessageSegments.Of(message))
            {
                if (seg.IsEmote)
                {
                    // A cluster no provider owns keeps a null Image.Url; TryResolve clears it on a miss.
                    TryResolve(seg.Text, out EmoteImage image);
                    segments.Add(new ChatSegment(seg.Text, image));
                }
                else
                {
                    SplitWords(seg.Text, segments); // a plain stretch: word emotes may hide in it
                }
            }
            return segments;
        }

        // break a run on spaces; a whole word a source owns becomes an emote, and re-add the spaces Split drops
        void SplitWords(string run, List<ChatSegment> segments)
        {
            var text = new StringBuilder();
            bool first = true;
            foreach (string word in run.Split(' '))
            {
                if (!first)
                {
                    text.Append(' ');
                }

                first = false;

                if (word.Length > 0 && TryResolve(word, out EmoteImage image))
                {
                    if (text.Length > 0)
                    {
                        segments.Add(new ChatSegment(text.ToString()));
                        text.Clear();
                    }
                    segments.Add(new ChatSegment(word, image));
                }
                else
                {
                    text.Append(word);
                }
            }
            if (text.Length > 0)
            {
                segments.Add(new ChatSegment(text.ToString()));
            }
        }

        // Twitch tag first, then emoji, then channel sets; a source whose toggle is off is skipped and its word stays text
        bool TryResolve(string text, out EmoteImage image)
        {
            if (cfg.showTwitchEmotes && twitch.TryResolve(text, out image)) return true;
            if (cfg.showEmoji && emoji.TryResolve(text, out image)) return true;
            if (cfg.showSevenTvEmotes && sevenTv.TryResolve(text, out image)) return true;
            if (cfg.showBttvEmotes && bttv.TryResolve(text, out image)) return true;
            if (cfg.showFfzEmotes && ffz.TryResolve(text, out image)) return true;

            image = default(EmoteImage);
            return false;
        }
    }
}
