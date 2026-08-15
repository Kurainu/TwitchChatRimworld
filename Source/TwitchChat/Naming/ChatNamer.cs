using System;
using System.Collections.Generic;
using System.Linq;
using TwitchChat.Chat;
using TwitchChat.Emotes;
using TwitchChat.Settings;
using RimWorld;
using Verse;

namespace TwitchChat.Naming;

// feeds chatters into the pool, auto-names eligible pawns, and shows each one's latest message; driven per second by ChatPulse
internal static class ChatNamer
{
	private static bool nameOther;
	private static bool nameWildAnimals;
	private static bool nameTameAnimals;
	private static bool reuseNames = true;

	// Cap renames per beat so a big backlog doesn't rename everything in one frame.
	private const int MaxNamesPerBatch = 10;

	private static int tickCounter;
	private static bool channelNudged;

	// Re-pick: link a pawn to a random recent chatter (the tab button).
	public static void ForceRename(Pawn pawn)
	{
		string login = ChatterPool.PeekRandomEternal();
		if (login != "")
		{
			TwitchLinks.Instance?.Link(pawn, login, ChatterPool.DisplayFor(login));
		}
	}

	public static void OnMessage(string login, string display, List<ChatSegment> segments)
	{
		ChatterPool.Add(login, display);
		ChatBubbles.Post(login, segments);
	}

	// called every tick by ChatPulse; the work below runs about once a second
	public static void Tick()
	{
		tickCounter = (tickCounter + 1) % 60;
		if (tickCounter != 0) return;

		// drain first so the passes below act on this tick's chat, not next tick's
		TwitchClient.CheckState();
		ChatBubbles.Show(TwitchLinks.Instance);
		GiveNames();
		RemindIfUnconfigured();

		var settings = LoadedModManager.GetMod<TwitchChatSettingsMod>().GetSettings<Settings.Settings>();
		reuseNames = settings.reuseNames;
		nameTameAnimals = settings.nameTamedAnimals;
		nameOther = settings.nameOther;
		nameWildAnimals = settings.nameWildAnimals;
		ChatterPool.SetCapacity(settings.chatterPoolSize);
	}

	private static void RemindIfUnconfigured()
	{
		if (channelNudged || TwitchClient.GetState() != TwitchClient.ConnectionState.NoChannel) return;

		channelNudged = true;
		Messages.Message("TwitchChat_Nudge_SetChannel".Translate(), MessageTypeDefOf.NeutralEvent, historical: false);
	}

	private static void GiveNames()
	{
		if (ChatterPool.ConsumedCount == 0) return;

		TwitchLinks store = TwitchLinks.Instance;
		Map map = Find.CurrentMap;
		if (store == null || map == null) return;

		int named = 0;
		foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned.ToList())
		{
			if (named >= MaxNamesPerBatch) break;
			if (store.IsLinked(pawn)) continue;

			try
			{
				if (Eligible(pawn) && LinkFromPool(store, pawn))
				{
					named++;
				}
			}
			catch (Exception ex)
			{
				Log.Error("[Twitch Chat] Failed to rename pawn: " + ex);
			}
		}
	}

	// each category gated by its own setting; colonists are never auto-named, only linked from the Twitch tab
	private static bool Eligible(Pawn pawn)
	{
		bool player = pawn.Faction == Faction.OfPlayer;
		if (pawn.RaceProps.Animal) return player ? nameTameAnimals : (pawn.Faction == null ? nameWildAnimals : nameOther);
		return !player && nameOther;
	}

	// link a free pool login of this pawn's kind; scans a snapshot so only the used login is removed
	private static bool LinkFromPool(TwitchLinks store, Pawn pawn)
	{
		bool forAnimal = pawn.AnimalOrWildMan();
		foreach (string login in ChatterPool.ConsumedSnapshot().InRandomOrder())
		{
			bool usedByAnimal = LinkedMatches(login, animal: true);
			bool usedByNonAnimal = LinkedMatches(login, animal: false);
			bool free = forAnimal
				? !usedByAnimal
				: !usedByNonAnimal && (!usedByAnimal || reuseNames);
			if (free)
			{
				ChatterPool.RemoveConsumed(login);
				store.Link(pawn, login, ChatterPool.DisplayFor(login));
				return true;
			}
		}
		return false;
	}

	private static bool LinkedMatches(string login, bool animal)
	{
		TwitchLinks store = TwitchLinks.Instance;
		if (store == null) return false;

		foreach (Pawn pawn in store.LinkedPawns)
		{
			if (pawn != null && pawn.Spawned && pawn.AnimalOrWildMan() == animal
				&& string.Equals(store.LoginFor(pawn), login, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}
		return false;
	}
}
