using HarmonyLib;
using Verse;

namespace TwitchChat;

// Applies the Harmony patches once when the game finishes loading.
[StaticConstructorOnStartup]
internal static class Mod
{
	public const string Id = "TwitchChat";

	static Mod()
	{
		Harmony harmony = new Harmony(Id);
		harmony.PatchAll();
		var v = typeof(Mod).Assembly.GetName().Version;
		Log.Message($"[Twitch Chat] v{v.Major}.{v.Minor}.{v.Build} initialized");
	}
}
