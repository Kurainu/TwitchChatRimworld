using System.Collections.Generic;
using System.Linq;
using Verse;

namespace TwitchChat.Settings;

// Persisted mod options. Edited in TwitchChatSettingsMod.DoSettingsWindowContents.
public class Settings : ModSettings
{
	// Twitch pawn-tab size; read by TwitchTab.
	public static float twitchTabHeight = 300f;
	public static float twitchTabWidth = 400f;

	// Connection. Channel only; the mod reads chat anonymously (no token/bot account).
	public string twitchChannel;

	// Automatic naming
	public bool nameTamedAnimals = true;
	public bool nameWildAnimals = true;
	public bool nameOther = true;
	public bool reuseNames = true;
	public int chatterPoolSize = 40;

	// Messages and bubbles
	public bool showSpeechBubbles = true;
	public bool showEmotes = true;
	public bool showTwitchEmotes = true;
	public bool showSevenTvEmotes = true;
	public bool showBttvEmotes = true;
	public bool showFfzEmotes = true;
	public bool showEmoji = true;
	public int messageShowDuration = 10;

	// Moderation
	public List<string> blacklist = new List<string>();
	public bool ignoreServiceBots = true;

	public override void ExposeData()
	{
		Scribe_Values.Look(ref twitchChannel, "twitchChannel");

		Scribe_Values.Look(ref nameTamedAnimals, "nameTamedAnimals", defaultValue: true);
		Scribe_Values.Look(ref nameWildAnimals, "nameWildAnimals", defaultValue: true);
		Scribe_Values.Look(ref nameOther, "nameOther", defaultValue: true);
		Scribe_Values.Look(ref reuseNames, "reuseNames", defaultValue: true);
		Scribe_Values.Look(ref chatterPoolSize, "chatterPoolSize", 40);

		Scribe_Values.Look(ref showSpeechBubbles, "showSpeechBubbles", defaultValue: true);
		Scribe_Values.Look(ref showEmotes, "showEmotes", defaultValue: true);
		Scribe_Values.Look(ref showTwitchEmotes, "showTwitchEmotes", defaultValue: true);
		Scribe_Values.Look(ref showSevenTvEmotes, "showSevenTvEmotes", defaultValue: true);
		Scribe_Values.Look(ref showBttvEmotes, "showBttvEmotes", defaultValue: true);
		Scribe_Values.Look(ref showFfzEmotes, "showFfzEmotes", defaultValue: true);
		Scribe_Values.Look(ref showEmoji, "showEmoji", defaultValue: true);
		Scribe_Values.Look(ref messageShowDuration, "messageShowDuration", 10);

		Scribe_Collections.Look(ref blacklist, "blacklist", LookMode.Value);
		Scribe_Values.Look(ref ignoreServiceBots, "ignoreServiceBots", defaultValue: true);

		if (Scribe.mode == LoadSaveMode.LoadingVars)
		{
			string legacy = null;
			Scribe_Values.Look(ref legacy, "chatterBlacklist");
			if ((blacklist == null || blacklist.Count == 0) && !string.IsNullOrEmpty(legacy))
			{
				blacklist = legacy.Split(',').Select(e => e.Trim()).Where(e => e.Length > 0).ToList();
			}
			blacklist ??= new List<string>();
		}
		base.ExposeData();
	}
}
