using TwitchChat.Interface;
using HarmonyLib;
using Verse.Profile;

namespace TwitchChat.Patch;

// clears bubbles on teardown; the socket is NOT closed here (also fires on in-game load, where dropping it killed the window), only on real quit
[HarmonyPatch(typeof(MemoryUtility), "ClearAllMapsAndWorld")]
internal static class Verse_Profile_MemoryUtility_ClearAllMapsAndWorld
{
	private static void Prefix()
	{
		Bubbler.Clear();
	}
}
