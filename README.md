# TwitchChatRimworld
Rimworld Mod for naming pawns (in game animal, raiders and visitors after Twitch chatters, shows their chat messages in bubbles with with Twitch, 7TV, BTTV, FFZ and
Unicode emote support. No Twitch Token or any authorization is needed!

## Layout

| Path | What it is |
|------|-----------|
| `modfiles/TwitchChatRimworld/` | The drop-in mod: `About/`, `Languages/`, `Patches/`, `Textures/`, `LoadFolders.xml`, and `1.6/Assemblies/TwitchChat.dll` |
| `Source/TwitchChat/` | SDK-style C# project (`net472`) that builds `TwitchChat.dll` against the 1.6 API |

## Download

Grab the ready-to-install mod from the **Actions** tab (latest build artifact) or
from **Releases** (tagged builds). Extract so you get a `TwitchChatRimworld`
folder inside your RimWorld `Mods` directory.

## Build

.NET Framework 4.7.2 or newer and Visual Studio 2022. No RimWorld install needed to compile. the first build
restores the reference packages from NuGet.

```powershell
cd Source\TwitchChat
dotnet build -c Release
```

The post-build step copies the freshly built `TwitchChat.dll` into
`modfiles\TwitchChatRimworld\1.6\Assemblies`, making that folder a complete and ready to use mod. `bin\Release\TwitchChat.dll` is the raw assembly.

Dependencies (restored from NuGet): `Krafs.Rimworld.Ref` 1.6.4871,
`Lib.Harmony` 2.3.6 ,
`Microsoft.NETFramework.ReferenceAssemblies`.

## Install

1. Build, or download the mod folder (see above).
2. Copy `TwitchChatRimworld` into your RimWorld `Mods` directory.
3. This mod needs
   [Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077)
   (`brrainz.harmony`); load it before Twitch Chat RimWorld.
4. Enable both in the Mods list and restart RimWorld.
5. Open **Mod Options -> Twitch Chat** and type your channel name and hit Test Connection. The
   status line goes to Connected once it connects.

## License

GPLv3; see `LICENSE`. If you distribute a modified version, you must also release
its full source under the same license. Portions derive from MIT-licensed work
(Jaxe-Dev's Interaction Bubbles); those notices are
retained, see `NOTICE`.

