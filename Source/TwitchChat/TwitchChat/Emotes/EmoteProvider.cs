namespace TwitchChat.Emotes
{
    public readonly struct EmoteImage
    {
        public readonly string Id;
        public readonly string Url;
        public EmoteImage(string id, string url) { Id = id; Url = url; }
    }

    public readonly struct ChatSegment
    {
        public readonly string Text;
        public readonly bool IsEmote;
        public readonly EmoteImage Image;

        public ChatSegment(string text) { Text = text; IsEmote = false; Image = default(EmoteImage); }
        public ChatSegment(string text, EmoteImage image) { Text = text; IsEmote = true; Image = image; }
    }

    public interface IEmoteProvider
    {
        void Load(string channelId);
        bool TryResolve(string text, out EmoteImage image);
    }
}
