using System;
using System.Collections.Generic;
using System.Linq;
using TwitchChat.Chat;
using UnityEngine;
using Verse;

namespace TwitchChat.Settings;

// The mod entry point Verse loads, and the options window it renders.
public class TwitchChatSettingsMod : Verse.Mod
{
	private static readonly Color OkColor = new Color(0.45f, 0.85f, 0.45f);
	private static readonly Color BadColor = new Color(0.9f, 0.55f, 0.5f);
	private static readonly Color WaitColor = new Color(0.9f, 0.8f, 0.45f);
	private static readonly Color NoteColor = new Color(0.6f, 0.8f, 1f);

	private readonly Settings settings;

	// fixed height a bit above the fully expanded panel (measured ~1160px, plus headroom for longer translations); measuring live column-breaks Listing_Standard and blanks the panel on a toggle
	private const float ContentHeight = 1250f;

	private static Vector2 scrollPosition = Vector2.zero;
	private static Vector2 blacklistScroll = Vector2.zero;
	private static string durationBuffer;
	private static string blacklistAddBuffer = "";

	public TwitchChatSettingsMod(ModContentPack content)
		: base(content)
	{
		settings = GetSettings<Settings>();
	}

	public override string SettingsCategory()
	{
		return "Twitch Chat";
	}

	public override void DoSettingsWindowContents(Rect inRect)
	{
		// pump the reader here; its per-second tick is paused with this window open, else status and last-message go stale
		TwitchClient.CheckState();

		if (durationBuffer == null)
		{
			durationBuffer = settings.messageShowDuration.ToString();
		}

		float viewWidth = inRect.width - 17f;
		Rect outRect = new Rect(inRect.x, inRect.y, viewWidth, inRect.height);
		Rect viewRect = new Rect(0f, 0f, viewWidth - 24f, ContentHeight);
		Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);

		Listing_Standard listing = new Listing_Standard();
		listing.Begin(viewRect);

		DrawConnectionSection(listing);
		DrawNamingSection(listing);
		DrawMessagesSection(listing);
		DrawCommandsSection(listing);

		listing.End();
		Widgets.EndScrollView();
		base.DoSettingsWindowContents(inRect);
	}

	private void DrawConnectionSection(Listing_Standard listing)
	{
		Header(listing, "TwitchChat_Set_ConnectionHeader".Translate());

		// Reassure: no account/token needed.
		GUI.color = NoteColor;
		listing.Label("TwitchChat_Set_AnonNote".Translate());
		GUI.color = Color.white;
		listing.Gap(4f);

		listing.Label("TwitchChat_Set_ChannelLabel".Translate());
		settings.twitchChannel = listing.TextEntry(settings.twitchChannel);
		listing.SubLabel("TwitchChat_Set_ChannelDesc".Translate(), 1f);

		listing.Gap(6f);

		TwitchClient.ConnectionState state = TwitchClient.GetState();
		bool connected = state == TwitchClient.ConnectionState.Connected;
		string statusText;
		Color statusColor;
		string hintKey;
		switch (state)
		{
		case TwitchClient.ConnectionState.NoChannel:
			statusText = "TwitchChat_Set_StatusNoChannel".Translate();
			statusColor = BadColor;
			hintKey = "TwitchChat_Set_HintNoChannel";
			break;
		case TwitchClient.ConnectionState.Connecting:
			statusText = "TwitchChat_Set_StatusConnecting".Translate();
			statusColor = WaitColor;
			hintKey = "TwitchChat_Set_HintConnecting";
			break;
		case TwitchClient.ConnectionState.Connected:
			statusText = "TwitchChat_Set_StatusConnected".Translate();
			statusColor = OkColor;
			hintKey = null;
			break;
		default:
			statusText = "TwitchChat_Set_StatusDisconnected".Translate();
			statusColor = BadColor;
			hintKey = "TwitchChat_Set_HintDisconnected";
			break;
		}

		GUI.color = statusColor;
		listing.Label("TwitchChat_Set_StatusLabel".Translate(statusText));
		GUI.color = Color.white;
		listing.SubLabel("TwitchChat_Set_ChannelValue".Translate(connected ? TwitchClient.CurrentChannel : "-"), 1f);
		listing.SubLabel("TwitchChat_Set_LastMessage".Translate(connected ? TwitchClient.LastMessage : "-"), 1f);
		if (hintKey != null)
		{
			listing.SubLabel(hintKey.Translate(), 1f);
		}
		if (listing.ButtonText("TwitchChat_Set_TestConnection".Translate()))
		{
			TwitchClient.Reconnect();
		}
		listing.SubLabel("TwitchChat_Set_TestConnectionDesc".Translate(), 1f);
	}

	private void DrawNamingSection(Listing_Standard listing)
	{
		Header(listing, "TwitchChat_Set_NamingHeader".Translate());
		listing.SubLabel("TwitchChat_Set_NamingIntro".Translate(), 1f);

		Toggle(listing, "TwitchChat_Set_NameOther".Translate(), ref settings.nameOther,
			"TwitchChat_Set_NameOtherOn".Translate(),
			"TwitchChat_Set_NameOtherOff".Translate());
		Toggle(listing, "TwitchChat_Set_NameWild".Translate(), ref settings.nameWildAnimals,
			"TwitchChat_Set_NameWildOn".Translate(),
			"TwitchChat_Set_NameWildOff".Translate());
		Toggle(listing, "TwitchChat_Set_NameTame".Translate(), ref settings.nameTamedAnimals,
			"TwitchChat_Set_NameTameOn".Translate(),
			"TwitchChat_Set_NameTameOff".Translate());
		Toggle(listing, "TwitchChat_Set_Reuse".Translate(), ref settings.reuseNames,
			"TwitchChat_Set_ReuseOn".Translate(),
			"TwitchChat_Set_ReuseOff".Translate());

		listing.Label("TwitchChat_Set_PoolSize".Translate(settings.chatterPoolSize));
		settings.chatterPoolSize = (int)listing.Slider(settings.chatterPoolSize, 5f, 200f);
		listing.SubLabel("TwitchChat_Set_PoolSizeDesc".Translate(), 1f);
	}

	private void DrawMessagesSection(Listing_Standard listing)
	{
		Header(listing, "TwitchChat_Set_MessagesHeader".Translate());

		Toggle(listing, "TwitchChat_Set_ShowBubbles".Translate(), ref settings.showSpeechBubbles,
			"TwitchChat_Set_ShowBubblesOn".Translate(),
			"TwitchChat_Set_ShowBubblesOff".Translate());
		Toggle(listing, "TwitchChat_Set_ShowEmotes".Translate(), ref settings.showEmotes,
			"TwitchChat_Set_ShowEmotesOn".Translate(),
			"TwitchChat_Set_ShowEmotesOff".Translate());
		if (settings.showEmotes)
		{
			listing.CheckboxLabeled("TwitchChat_Set_EmoteTwitch".Translate(), ref settings.showTwitchEmotes);
			listing.CheckboxLabeled("TwitchChat_Set_EmoteSevenTv".Translate(), ref settings.showSevenTvEmotes);
			listing.CheckboxLabeled("TwitchChat_Set_EmoteBttv".Translate(), ref settings.showBttvEmotes);
			listing.CheckboxLabeled("TwitchChat_Set_EmoteFfz".Translate(), ref settings.showFfzEmotes);
			listing.CheckboxLabeled("TwitchChat_Set_EmoteEmoji".Translate(), ref settings.showEmoji);
		}

		listing.Gap(6f);
		listing.Label("TwitchChat_Set_BubbleDuration".Translate(settings.messageShowDuration));
		listing.TextFieldNumeric(ref settings.messageShowDuration, ref durationBuffer, 1f, 60f);
		listing.SubLabel("TwitchChat_Set_BubbleDurationDesc".Translate(), 1f);
	}

	private void DrawCommandsSection(Listing_Standard listing)
	{
		Header(listing, "TwitchChat_Set_ModerationHeader".Translate());

		Toggle(listing, "TwitchChat_Set_IgnoreBots".Translate(), ref settings.ignoreServiceBots,
			"TwitchChat_Set_IgnoreBotsOn".Translate(),
			"TwitchChat_Set_IgnoreBotsOff".Translate());

		listing.Label("TwitchChat_Set_Blacklist".Translate());
		listing.SubLabel("TwitchChat_Set_BlacklistDesc".Translate(), 1f);

		const float buttonWidth = 90f;
		Rect addRow = listing.GetRect(28f);
		Rect fieldRect = new Rect(addRow.x, addRow.y, addRow.width - buttonWidth - 6f, addRow.height);
		Rect addButton = new Rect(fieldRect.xMax + 6f, addRow.y, buttonWidth, addRow.height);
		blacklistAddBuffer = Widgets.TextField(fieldRect, blacklistAddBuffer);
		if (Widgets.ButtonText(addButton, "TwitchChat_Set_BlacklistAddBtn".Translate()))
		{
			AddBlacklistEntry(blacklistAddBuffer);
			blacklistAddBuffer = "";
		}

		listing.Gap(4f);

		List<string> entries = settings.blacklist;
		if (entries.Count == 0)
		{
			listing.SubLabel("TwitchChat_Set_BlacklistEmpty".Translate(), 1f);
			return;
		}

		// Inner scroll view so a long blacklist doesn't overflow the fixed-height panel.
		const float rowHeight = 26f;
		const float maxListHeight = 170f;
		float contentHeight = entries.Count * rowHeight;
		Rect listOuter = listing.GetRect(Mathf.Min(contentHeight, maxListHeight));
		Rect listView = new Rect(0f, 0f, listOuter.width - 16f, contentHeight);
		Widgets.BeginScrollView(listOuter, ref blacklistScroll, listView);

		string toRemove = null;
		float rowY = 0f;
		foreach (string entry in entries)
		{
			Rect nameRect = new Rect(4f, rowY, listView.width - buttonWidth - 10f, rowHeight);
			Rect removeButton = new Rect(nameRect.xMax + 6f, rowY, buttonWidth, rowHeight);
			Widgets.Label(nameRect, entry);
			if (Widgets.ButtonText(removeButton, "TwitchChat_Set_BlacklistRemoveBtn".Translate()))
			{
				toRemove = entry; // remove after the loop
			}
			rowY += rowHeight;
		}
		Widgets.EndScrollView();

		if (toRemove != null)
		{
			entries.Remove(toRemove);
		}
	}

	// Add to blacklist, skipping blanks and dupes.
	private void AddBlacklistEntry(string raw)
	{
		string name = raw?.Trim();
		if (string.IsNullOrEmpty(name)) return;
		if (!settings.blacklist.Any(e => string.Equals(e, name, StringComparison.OrdinalIgnoreCase)))
		{
			settings.blacklist.Add(name);
		}
	}

	// Checkbox with an on/off-dependent sublabel.
	private static void Toggle(Listing_Standard listing, string label, ref bool value, string onText, string offText)
	{
		listing.CheckboxLabeled(label, ref value);
		listing.SubLabel(value ? onText : offText, 1f);
	}

	private static void Header(Listing_Standard listing, string title)
	{
		listing.Gap(10f);
		Text.Font = GameFont.Medium;
		listing.Label(title);
		Text.Font = GameFont.Small;
		listing.GapLine(2f);
	}
}
