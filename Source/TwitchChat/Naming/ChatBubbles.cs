using System;
using System.Collections.Generic;
using System.Linq;
using TwitchChat.Emotes;
using TwitchChat.Interface;
using Verse;

namespace TwitchChat.Naming;


internal static class ChatBubbles
{
	private static readonly object gate = new object();
	private static readonly Dictionary<string, List<ChatSegment>> latest = new Dictionary<string, List<ChatSegment>>(StringComparer.OrdinalIgnoreCase);

	public static void Post(string login, List<ChatSegment> segments)
	{
		lock (gate)
		{
			latest[login] = segments;
		}
	}

	public static void Show(TwitchLinks store)
	{
		if (store != null)
		{
			foreach (Pawn pawn in store.LinkedPawns.ToList())
			{
				if (pawn == null || !pawn.Spawned || pawn.Map != Find.CurrentMap) continue;
				string login = store.LoginFor(pawn);
				if (login == null) continue;

				List<ChatSegment> segments;
				lock (gate)
				{
					if (!latest.TryGetValue(login, out segments)) continue;
				}
				Bubbler.AddBubble(pawn, segments);
			}
		}
		lock (gate)
		{
			latest.Clear();
		}
	}
}
