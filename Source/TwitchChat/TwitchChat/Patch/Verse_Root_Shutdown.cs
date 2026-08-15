using HarmonyLib;
using TwitchChat.Chat;
using Verse;

namespace TwitchChat.Patch;

// Close the Twitch socket on app quit. Menu-return/load is handled by the ClearAllMapsAndWorld patch.
[HarmonyPatch(typeof(Root), nameof(Root.Shutdown))]
internal static class Verse_Root_Shutdown
{
	private static void Prefix()
	{
		TwitchClient.Shutdown();
	}
}
