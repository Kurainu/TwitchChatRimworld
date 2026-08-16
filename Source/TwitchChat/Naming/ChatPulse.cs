using TwitchChat.Chat;
using Verse;

namespace TwitchChat.Naming;

// drives ChatNamer.Tick once per game tick (ChatNamer throttles to ~1s); a GameComponent so it fires once no matter how many maps exist and keeps running on the world/caravan view
public class ChatPulse : GameComponent
{
	public ChatPulse(Game game)
	{
	}

	public override void FinalizeInit()
	{
		TwitchClient.AutoConnect();
	}

	public override void GameComponentTick()
	{
		ChatNamer.Tick();
	}
}
