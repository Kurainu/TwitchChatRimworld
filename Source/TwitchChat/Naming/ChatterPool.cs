using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace TwitchChat.Naming;

// recent chatters by login: consumed is drawn down for naming, eternal keeps the last N for the picker; all access locked
internal static class ChatterPool
{
	private static int maxTracked = 40;

	private static readonly object gate = new object();
	private static readonly List<string> consumed = new List<string>();
	private static readonly List<string> eternal = new List<string>();
	private static readonly Dictionary<string, string> displays = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

	// Logins are case-insensitive; keep the first casing seen, no duplicates.
	private static bool ContainsIgnoreCase(List<string> list, string login)
	{
		return list.Any(x => string.Equals(x, login, StringComparison.OrdinalIgnoreCase));
	}

	// Record a chatter by login and its latest display (from the read thread).
	public static void Add(string login, string display)
	{
		if (string.IsNullOrEmpty(login)) return;

		lock (gate)
		{
			if (!ContainsIgnoreCase(consumed, login))
			{
				consumed.Add(login);
			}
			if (!ContainsIgnoreCase(eternal, login))
			{
				eternal.Add(login);
			}
			displays[login] = string.IsNullOrEmpty(display) ? login : display;

			if (consumed.Count > maxTracked)
			{
				DropOldest(consumed);
			}
			if (eternal.Count > maxTracked)
			{
				DropOldest(eternal);
			}
		}
	}

	public static string DisplayFor(string login)
	{
		if (string.IsNullOrEmpty(login)) return "";
		lock (gate)
		{
			return displays.TryGetValue(login, out string d) ? d : login;
		}
	}

	// drop every tracked chatter, e.g. when the channel changes
	public static void Clear()
	{
		lock (gate)
		{
			consumed.Clear();
			eternal.Clear();
			displays.Clear();
		}
	}

	public static void SetCapacity(int size)
	{
		lock (gate)
		{
			maxTracked = size < 1 ? 1 : size;
			while (consumed.Count > maxTracked)
			{
				DropOldest(consumed);
			}
			while (eternal.Count > maxTracked)
			{
				DropOldest(eternal);
			}
		}
	}

	public static int ConsumedCount
	{
		get
		{
			lock (gate)
			{
				return consumed.Count;
			}
		}
	}

	// A copy of the consumable pool for a non-destructive scan; the caller removes only the one it uses.
	public static List<string> ConsumedSnapshot()
	{
		lock (gate)
		{
			return new List<string>(consumed);
		}
	}

	// drop from the consumable pool once assigned; its display stays, eternal still references it
	public static void RemoveConsumed(string login)
	{
		lock (gate)
		{
			int i = consumed.FindIndex(x => string.Equals(x, login, StringComparison.OrdinalIgnoreCase));
			if (i >= 0)
			{
				consumed.RemoveAt(i);
			}
		}
	}

	public static string PeekRandomEternal()
	{
		lock (gate)
		{
			if (eternal.Count == 0) return "";
			return eternal[Rand.Range(0, eternal.Count)];
		}
	}

	public static List<string> Snapshot()
	{
		lock (gate)
		{
			return new List<string>(eternal);
		}
	}

	// Drop the oldest entry, forgetting its display only if nothing else still references it.
	private static void DropOldest(List<string> list)
	{
		string login = list[0];
		list.RemoveAt(0);
		if (!ContainsIgnoreCase(consumed, login) && !ContainsIgnoreCase(eternal, login))
		{
			displays.Remove(login);
		}
	}
}
