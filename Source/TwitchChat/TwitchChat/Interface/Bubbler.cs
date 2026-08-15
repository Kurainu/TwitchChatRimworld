using System.Collections.Generic;
using System.Linq;
using TwitchChat.Compatibility;
using TwitchChat.Emotes;
using TwitchChat.Patch;
using TwitchChat.Settings;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace TwitchChat.Interface;

/// <summary>Owns the on-map speech bubbles: queues them per pawn and draws them each frame.</summary>
internal static class Bubbler
{
	// Reference zoom/height the scaling maths were tuned against.
	private const float OptimalZoom = 45f;
	private const float OptimalMinZoom = 8f;
	private const float OptimalHeight = 1050f;

	private static readonly Dictionary<Pawn, List<Bubble>> bubbles = new Dictionary<Pawn, List<Bubble>>();

	public static bool Visibility { get; set; } = true;

	public static bool CanShow
	{
		get
		{
			if (Theme.Activated && Visibility)
			{
				return !WorldRendererUtility.WorldRendered;
			}
			return false;
		}
	}

	public static void AddBubble(Pawn pawn, List<ChatSegment> segments)
	{
		if (!LoadedModManager.GetMod<TwitchChatSettingsMod>().GetSettings<TwitchChat.Settings.Settings>().showSpeechBubbles)
		{
			return;
		}

		Faction faction = pawn.Faction;
		bool isPlayer = faction != null && faction.IsPlayer;
		if (!isPlayer && !Theme.DoNonPlayer)
		{
			return;
		}

		bool isAnimal = (pawn?.RaceProps?.Animal).GetValueOrDefault();
		if (isAnimal && !Theme.DoAnimals)
		{
			return;
		}

		if (pawn == null || pawn.Map != Find.CurrentMap)
		{
			return;
		}

		if (!bubbles.ContainsKey(pawn))
		{
			bubbles[pawn] = new List<Bubble>();
		}
		bubbles[pawn].Add(new Bubble(segments, combat: false));
	}

	private static void Remove(Pawn pawn, Bubble bubble)
	{
		bubbles[pawn].Remove(bubble);
		if (bubbles[pawn].Count == 0)
		{
			bubbles.Remove(pawn);
		}
	}

	private static float GetScale()
	{
		float heightRatio = UI.screenHeight / OptimalHeight;
		float maxZoom = (CameraPlus.Loaded ? CameraPlus.CameraPlusOptimalZoom : OptimalZoom) * heightRatio;
		float minZoom = OptimalMinZoom * heightRatio;
		float span = Mathf.Max((maxZoom - minZoom) * Theme.ScaleStart.ToPercentageFloat(), 1f);
		return Mathf.Min((Find.CameraDriver.CellSizePixels - minZoom) / span, 1f);
	}

	public static void Draw()
	{
		if (!CanShow)
		{
			return;
		}

		float scale = GetScale();
		if (scale <= Theme.MinScale.ToPercentageFloat())
		{
			return;
		}

		Pawn selected = Find.Selector.SingleSelectedObject as Pawn;

		// Draw back-to-front by map row, then draw the selected pawn last (on top).
		Pawn[] ordered = (from pawn in bubbles.Keys
			orderby pawn.Position.y
			where pawn != selected
			select pawn).ToArray();
		foreach (Pawn pawn in ordered)
		{
			DrawBubble(pawn, isSelected: false, scale);
		}

		if (selected != null && bubbles.ContainsKey(selected))
		{
			DrawBubble(selected, isSelected: true, scale);
		}
	}

	private static void DrawBubble(Pawn pawn, bool isSelected, float scale)
	{
		if (!pawn.Spawned || pawn.Map != Find.CurrentMap
			|| pawn.Map.fogGrid.IsFogged(pawn.Position) || pawn.IsPsychologicallyInvisible())
		{
			return;
		}

		Vector2 anchor = GenMapUI.LabelDrawPosFor(pawn, -0.6f);
		int offset = Mathf.CeilToInt(Theme.StartOffset * scale);
		int drawn = 0;

		foreach (Bubble bubble in bubbles[pawn].ToArray())
		{
			if (drawn > Theme.MaxPerPawn)
			{
				break;
			}
			if (!bubble.Draw(anchor + GetOffset(offset), isSelected, scale))
			{
				Remove(pawn, bubble);
			}
			offset += (Theme.GetOffsetDirection().IsHorizontal ? bubble.Width : bubble.Height) + Theme.Spacing;
			drawn++;
		}
	}

	private static Vector2 GetOffset(int offset)
	{
		Vector2 direction = Theme.GetOffsetDirection().AsVector2;
		return new Vector2(offset * direction.x, offset * direction.y);
	}

	public static void Clear()
	{
		bubbles.Clear();
	}
}
