using System.Collections.Concurrent;
using Verse;

namespace TwitchChat.Emotes
{
    // emote loading runs off-thread but RimWorld's Log is main-thread-only; dev messages queue here for TwitchClient.CheckState to flush on the game thread
    static class EmoteLog
    {
        static readonly ConcurrentQueue<string> pending = new ConcurrentQueue<string>();

        public static void Dev(string message)
        {
            if (Prefs.DevMode) pending.Enqueue(message);
        }

        public static void Flush()
        {
            while (pending.TryDequeue(out string message)) Log.Message(message);
        }
    }
}
