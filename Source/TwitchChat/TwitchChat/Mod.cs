using HarmonyLib;
using Verse;

namespace TwitchChat;

// Applies the Harmony patches once when the game finishes loading.
[StaticConstructorOnStartup]
internal static class Mod
{
	public const string Id = "TwitchChat";

	// Logged on startup to identify the loaded build.
	public const string Version = "2.0.0";

	static Mod()
	{
		Harmony harmony = new Harmony(Id);
		harmony.PatchAll();
		Log.Message($"[Twitch Chat] v{Version} initialized");
	}
}
