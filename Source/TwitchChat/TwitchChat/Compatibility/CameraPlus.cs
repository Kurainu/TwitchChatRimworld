using System.Reflection;
using Verse;

namespace TwitchChat.Compatibility;

internal static class CameraPlus
{
	public const float CameraPlusOptimalZoom = 22.5f;

	public static readonly bool Loaded = LoadedModManager.RunningModsListForReading.FirstOrDefault((ModContentPack mod) => mod.Name == "Camera+")?.assemblies.loadedAssemblies.FirstOrDefault((Assembly assembly) => assembly.GetName().Name == "CameraPlus") != null;
}
