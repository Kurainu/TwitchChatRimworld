using System.Collections.Generic;
using System.Linq;
using TwitchChat.Naming;
using UnityEngine;
using Verse;

namespace TwitchChat.Interface;

// Searchable list of recent chatters; picking one links it to the pawn.
public class Dialog_PickChatter : Window
{
	private const float RowHeight = 30f;

	private readonly Pawn pawn;
	private string search = "";
	private Vector2 scroll = Vector2.zero;

	public Dialog_PickChatter(Pawn pawn)
	{
		this.pawn = pawn;
		doCloseX = true;
		closeOnClickedOutside = true;
		absorbInputAroundWindow = true;
		draggable = true;
	}

	public override Vector2 InitialSize => new Vector2(360f, 480f);

	public override void DoWindowContents(Rect inRect)
	{
		Text.Font = GameFont.Medium;
		Rect titleRect = new Rect(inRect.x, inRect.y, inRect.width, 34f);
		Widgets.Label(titleRect, "TwitchChat_PickChatterBtn".Translate());
		Text.Font = GameFont.Small;

		float y = titleRect.yMax + 4f;
		Rect searchRect = new Rect(inRect.x, y, inRect.width, 28f);
		search = Widgets.TextField(searchRect, search);
		y = searchRect.yMax + 6f;

		var logins = ChatterPool.Snapshot();
		logins.Reverse(); // most-recent first
		if (!string.IsNullOrEmpty(search))
		{
			string needle = search.ToLowerInvariant();
			logins = logins.Where(login =>
				login.ToLowerInvariant().Contains(needle)
				|| ChatterPool.DisplayFor(login).ToLowerInvariant().Contains(needle)).ToList();
		}

		Rect listRect = new Rect(inRect.x, y, inRect.width, inRect.yMax - y);
		if (logins.Count == 0)
		{
			Widgets.Label(listRect, "TwitchChat_PickEmpty".Translate());
			return;
		}

		Rect viewRect = new Rect(0f, 0f, listRect.width - 16f, logins.Count * RowHeight);
		Widgets.BeginScrollView(listRect, ref scroll, viewRect);
		float rowY = 0f;
		foreach (string login in logins)
		{
			string shown = ChatterPool.DisplayFor(login);
			Rect row = new Rect(0f, rowY, viewRect.width, RowHeight);
			if (Widgets.ButtonText(row, shown))
			{
				TwitchLinks.Instance?.Link(pawn, login, shown);
				Close();
				break;
			}
			rowY += RowHeight;
		}
		Widgets.EndScrollView();
	}
}
