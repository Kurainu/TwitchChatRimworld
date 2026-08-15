using System;
using TwitchChat.Settings;
using RimWorld;
using TwitchChat.Naming;
using UnityEngine;
using Verse;

namespace TwitchChat.Interface;

// Inspector tab that shows a pawn's chatter link and lets the player repick/link/unlink it.
public class TwitchTab : ITab_Pawn_Gear
{
	private const float TopPadding = 8f;
	private const float LeftPadding = 10f;
	private const float RowHeight = 24f;

	public override bool IsVisible => true;

	private Pawn SelPawnForGear =>
		base.SelPawn
		?? (base.SelThing is Corpse corpse
			? corpse.InnerPawn
			: throw new InvalidOperationException("Gear tab on non-pawn non-corpse " + base.SelThing));

	public TwitchTab()
	{
		size = new Vector2(TwitchChat.Settings.Settings.twitchTabWidth, TwitchChat.Settings.Settings.twitchTabHeight);
		labelKey = "TwitchChat_TabTwitch";
		tutorTag = "Twitch";
	}

	protected override void UpdateSize()
	{
		size = new Vector2(TwitchChat.Settings.Settings.twitchTabWidth, TwitchChat.Settings.Settings.twitchTabHeight);
	}

	protected override void FillTab()
	{
		Rect group = new Rect(0f, 0f, size.x, size.y).ContractedBy(LeftPadding);
		GUI.BeginGroup(group);
		GUI.color = Color.white;

		float contentWidth = group.width - LeftPadding;
		Rect row = new Rect(LeftPadding, TopPadding, contentWidth, RowHeight);

		string login = TwitchLinks.Instance?.LoginFor(SelPawnForGear);
		string linkedName = string.IsNullOrEmpty(login) ? "-" : ChatterPool.DisplayFor(login);

		Text.Font = GameFont.Medium;
		Widgets.Label(row, "TwitchChat_LinkedTo".Translate() + ": " + linkedName);
		Text.Font = GameFont.Small;

		row.y += RowHeight + 24f;
		row.width = 135f;
		if (Widgets.ButtonText(row, "TwitchChat_RepickBtn".Translate()))
		{
			ChatNamer.ForceRename(SelPawnForGear);
		}

		row.y += RowHeight;
		row.width = contentWidth;
		Widgets.Label(row, "TwitchChat_RepickDescr".Translate());

		row.y += RowHeight + 16f;
		row.width = 135f;
		if (Widgets.ButtonText(row, "TwitchChat_PickChatterBtn".Translate()))
		{
			Find.WindowStack.Add(new Dialog_PickChatter(SelPawnForGear));
		}

		row.y += RowHeight;
		row.width = contentWidth;
		row.height = RowHeight * 2f;
		Widgets.Label(row, "TwitchChat_PickChatterDescr".Translate());

		row.y += RowHeight + 8f;
		row.y += RowHeight;
		row.width = 135f;
		row.height = RowHeight;
		if (Widgets.ButtonText(row, "TwitchChat_RemoveLinkBtn".Translate()))
		{
			TwitchLinks.Instance?.Unlink(SelPawnForGear);
		}

		row.y += RowHeight;
		row.width = contentWidth;
		row.height = RowHeight * 2f;
		Widgets.Label(row, "TwitchChat_RemoveLinkDescr".Translate());

		GUI.EndGroup();
		GUI.color = Color.white;
		Text.Anchor = TextAnchor.UpperLeft;
	}
}
