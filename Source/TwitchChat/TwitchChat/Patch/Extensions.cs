using UnityEngine;

namespace TwitchChat.Patch;

internal static class Extensions
{
	public static string Italic(this string self)
	{
		return "<i>" + self + "</i>";
	}

	public static string Bold(this string self)
	{
		return "<b>" + self + "</b>";
	}

	public static Color CWithAlpha(this Color self, float alpha)
	{
		return new Color(self.r, self.g, self.b, alpha);
	}

	public static Rect ContractedByCoords(this Rect self, float x, float y)
	{
		return new Rect(self.x + x, self.y + y, self.width - x * 2f, self.height - y * 2f);
	}

	public static float ToPercentageFloat(this int self)
	{
		return (float)self / 100f;
	}
}
