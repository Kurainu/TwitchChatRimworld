using HarmonyLib;
using TwitchChat.Naming;
using Verse;

namespace TwitchChat.Patch;

// Drops a destroyed pawn from TwitchLinks so the save never references dead pawns.
[HarmonyPatch(typeof(Pawn), nameof(Pawn.Destroy))]
internal static class Verse_Pawn_Destroy
{
	private static void Postfix(Pawn __instance)
	{
		TwitchLinks.Instance?.Forget(__instance);
	}
}
