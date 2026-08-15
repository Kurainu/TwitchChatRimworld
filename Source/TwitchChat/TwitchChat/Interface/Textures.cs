using UnityEngine;
using Verse;

namespace TwitchChat.Interface;

[StaticConstructorOnStartup]
internal static class Textures
{
	public static readonly Texture2D Inner = ContentFinder<Texture2D>.Get("TwitchChat/Inner");

	public static readonly Texture2D Outer = ContentFinder<Texture2D>.Get("TwitchChat/Outer");
}
