using System.Collections.Generic;
using TwitchChat.Emotes;
using TwitchChat.Patch;
using TwitchChat.Settings;
using UnityEngine;
using Verse;
using ModConfig = TwitchChat.Settings.Settings;

namespace TwitchChat.Interface;

// a speech bubble: its parsed segments plus the timing and geometry to draw and fade it
internal class Bubble
{
	// Reference emote size in pixels (Twitch/FFZ draw emotes at 28x28); scaled with the bubble zoom.
	private const float EmoteSize = 28f;

	private readonly List<ChatSegment> _segments;
	private readonly bool _combat;
	private float _fadeStartTime;
	private float _fadeLength;

	public int Width { get; private set; }

	public int Height { get; private set; }

	public Bubble(List<ChatSegment> segments, bool combat)
	{
		_segments = segments;
		_combat = combat;
		_fadeLength = -1f;
		_fadeStartTime = -1f;
	}

	private static ModConfig Config => LoadedModManager.GetMod<TwitchChatSettingsMod>().GetSettings<ModConfig>();

	// opacity as the bubble ages: full for the first half of its life, then fades to zero
	private float GetFade()
	{
		if (_fadeLength == -1f)
		{
			_fadeLength = Config.messageShowDuration;
		}
		if (_fadeStartTime == -1f)
		{
			_fadeStartTime = Time.time;
			return Theme.StartOpacity.ToPercentageFloat();
		}

		float age = Time.time - _fadeStartTime;
		const float holdFraction = 0.5f;
		if (age <= 0f || age < _fadeLength * holdFraction)
		{
			return Theme.StartOpacity.ToPercentageFloat();
		}
		return Theme.StartOpacity.ToPercentageFloat() * (1f - (age - _fadeLength * holdFraction) / (_fadeLength * holdFraction));
	}

	private Color GetColor(bool foreground, bool isSelected)
	{
		if (isSelected)
		{
			if (_combat)
			{
				return foreground ? Theme.CombatSelectedForeColor : Theme.CombatSelectedBackColor;
			}
			return foreground ? Theme.SelectedForeColor : Theme.SelectedBackColor;
		}
		if (_combat)
		{
			return foreground ? Theme.CombatForeColor : Theme.CombatBackColor;
		}
		return foreground ? Theme.ForeColor : Theme.BackColor;
	}

	// draw the bubble; returns false once it has fully faded and should be dropped
	public bool Draw(Vector2 pos, bool isSelected, float scale)
	{
		Rot4 direction = Theme.GetOffsetDirection();
		GUIStyle font = Theme.GetFont(scale);
		int padX = Mathf.CeilToInt(Theme.PaddingX * scale);
		int padY = Mathf.CeilToInt(Theme.PaddingY * scale);
		int maxWidth = Mathf.CeilToInt(Theme.MaxWidth * scale);

		// lay the message on one line: emote images where a texture is ready, else text (the emote name until it loads, or every emote when showEmotes is off)
		bool showEmotes = Config.showEmotes;
		// left-aligned non-wrapping copy for inline runs; the base style centers and wraps, which would push a run onto a clipped second line
		GUIStyle textStyle = new GUIStyle(font) { wordWrap = false, alignment = TextAnchor.MiddleLeft };
		float lineHeight = textStyle.CalcSize(new GUIContent("Ag")).y;
		float emoteSize = EmoteSize * scale;
		int maxContent = maxWidth - padX * 2;

		var pieces = new List<(Texture2D image, string text, float width)>();
		float contentWidth = 0f;
		bool hasEmote = false;
		foreach (ChatSegment seg in _segments)
		{
			Texture2D image = showEmotes && seg.IsEmote ? EmoteCache.Get(seg.Image.Url) : null;
			if (image != null)
			{
				pieces.Add((image, null, emoteSize));
				contentWidth += emoteSize;
				hasEmote = true;
			}
			else
			{
				string text = seg.Text.StripTags();
				float width = textStyle.CalcSize(new GUIContent(text)).x;
				pieces.Add((null, text, width));
				contentWidth += width;
			}
			if (contentWidth >= maxContent)
			{
				contentWidth = maxContent;
				break; // single line for now; the rest is clipped
			}
		}

		// Rows with an emote grow to the emote height so nothing clips; text-only rows stay compact.
		float rowHeight = hasEmote ? Mathf.Max(emoteSize, lineHeight) : lineHeight;
		Width = Mathf.CeilToInt(contentWidth + padX * 2); // contentWidth is already capped at maxContent
		Height = Mathf.CeilToInt(rowHeight + padY * 2);

		float x = pos.x;
		float y = pos.y;
		if (direction.IsHorizontal)
		{
			y -= Height / 2f;
		}
		else
		{
			x -= Width / 2f;
		}

		Rect outer = new Rect(Mathf.CeilToInt(x), Mathf.CeilToInt(y), Width, Height);
		Rect inner = outer.ContractedByCoords(padX, padY);

		float alpha = Mathf.Min(GetFade(), Mouse.IsOver(outer) ? Theme.MouseOverOpacity.ToPercentageFloat() : 1f);
		if (alpha <= 0f)
		{
			return false;
		}

		Color background = GetColor(foreground: false, isSelected).CWithAlpha(alpha);
		Color foreground = GetColor(foreground: true, isSelected).CWithAlpha(alpha);
		Color imageTint = new Color(1f, 1f, 1f, alpha); // don't recolor emotes, only fade them
		Color previousColor = GUI.color;

		GUI.color = background;
		Widgets.DrawAtlas(outer, Textures.Inner);
		GUI.color = foreground;
		Widgets.DrawAtlas(outer, Textures.Outer);

		float dx = inner.x;
		foreach ((Texture2D image, string text, float width) in pieces)
		{
			if (image != null)
			{
				GUI.color = imageTint;
				GUI.DrawTexture(new Rect(dx, inner.y, emoteSize, emoteSize), image, ScaleMode.ScaleToFit);
			}
			else
			{
				GUI.color = foreground;
				// clip the run to what's left of the bubble so it stays on one line; the row is emote-tall, so text centers next to the emotes
				GUI.Label(new Rect(dx, inner.y, Mathf.Min(width, inner.xMax - dx), rowHeight), text, textStyle);
			}
			dx += width;
		}

		GUI.color = previousColor;
		return true;
	}
}
