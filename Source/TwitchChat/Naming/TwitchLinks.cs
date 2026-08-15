using System.Collections.Generic;
using System.Linq;
using Verse;

namespace TwitchChat.Naming;

public class TwitchLinks : GameComponent
{
	private Dictionary<Pawn, string> links = new Dictionary<Pawn, string>();
	private Dictionary<Pawn, Name> originalNames = new Dictionary<Pawn, Name>();

	// Scribe working lists for the reference/deep collection modes.
	private List<Pawn> linkPawnsWork;
	private List<string> linkChatterWork;
	private List<Pawn> namePawnsWork;
	private List<Name> nameValueWork;

	public TwitchLinks(Game game)
	{
	}

	public static TwitchLinks Instance => Current.Game?.GetComponent<TwitchLinks>();

	public bool IsLinked(Pawn pawn) => pawn != null && links.ContainsKey(pawn);

	// The chatter's login (stable identity) linked to a pawn, used to route messages.
	public string LoginFor(Pawn pawn) => pawn != null && links.TryGetValue(pawn, out string c) ? c : null;

	public IEnumerable<Pawn> LinkedPawns => links.Keys;

	// Link a pawn to a chatter's login and show its display name; remembers the real name for unlink.
	public void Link(Pawn pawn, string login, string display)
	{
		if (pawn == null || string.IsNullOrEmpty(login)) return;

		if (!originalNames.ContainsKey(pawn))
		{
			originalNames[pawn] = pawn.Name;
		}
		links[pawn] = login;
		string shown = string.IsNullOrEmpty(display) ? login : display;
		pawn.Name = DisplayName(pawn, shown);

		if (Prefs.DevMode)
		{
			Log.Message($"[Twitch Chat] named a {pawn.def.label} after {shown} (login {login})");
		}
	}

	public void Unlink(Pawn pawn)
	{
		if (pawn == null) return;

		string login = LoginFor(pawn);

		if (originalNames.TryGetValue(pawn, out Name original))
		{
			pawn.Name = original;
		}
		else if (pawn.RaceProps.Animal)
		{
			pawn.Name = null; // nameless animal falls back to its kind label
		}

		links.Remove(pawn);
		originalNames.Remove(pawn);

		if (login != null && Prefs.DevMode)
		{
			Log.Message($"[Twitch Chat] unlinked a {pawn.def.label} from {login}, restored to {pawn.LabelShort}");
		}
	}

	public void Forget(Pawn pawn)
	{
		if (pawn == null) return;

		links.Remove(pawn);
		originalNames.Remove(pawn);
	}

	// Animals get a NameSingle; humanlikes keep First/Last, only the Nick is swapped.
	private static Name DisplayName(Pawn pawn, string shown)
	{
		if (pawn.RaceProps.Animal) return new NameSingle(shown);
		if (pawn.Name is NameTriple triple) return new NameTriple(triple.First, shown, triple.Last);
		return new NameSingle(shown);
	}

	public override void ExposeData()
	{
		Scribe_Collections.Look(ref links, "twitchLinks", LookMode.Reference, LookMode.Value, ref linkPawnsWork, ref linkChatterWork);
		Scribe_Collections.Look(ref originalNames, "twitchOriginalNames", LookMode.Reference, LookMode.Deep, ref namePawnsWork, ref nameValueWork);

		if (Scribe.mode != LoadSaveMode.PostLoadInit) return;

		links ??= new Dictionary<Pawn, string>();
		originalNames ??= new Dictionary<Pawn, Name>();

		// drop dead links; old saves stored the display here, so lower-case it to the login (routing key only, not the shown name)
		foreach (Pawn p in links.Keys.ToList())
		{
			if (p == null || links[p] == null)
			{
				links.Remove(p);
				continue;
			}
			string lowered = links[p].ToLowerInvariant();
			if (lowered != links[p])
			{
				links[p] = lowered;
			}
		}
		foreach (Pawn p in originalNames.Keys.ToList())
		{
			if (p == null)
			{
				originalNames.Remove(p);
			}
		}
	}
}
