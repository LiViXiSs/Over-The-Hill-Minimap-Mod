# Over the Hill Minimap

A client-side minimap mod for **over the hill**, built with MelonLoader. Keep a map in the corner of your screen while driving, with adjustable zoom and live updates.

**Current version: 0.2.1 — experimental.** Position alignment, icon visibility, roads/trails, and multiplayer markers are still being tested. This version includes fixes for previously reported alignment and missing-icon issues; those fixes have not yet been verified in-game.

## Features

- North-up minimap in the upper-right corner, centered on your vehicle.
- Adjustable zoom and map size.
- Uses the game's map imagery and fog of war.
- Mirrors the normal map's icon sprites, colors, rotation, and visibility.
- Refreshes player indicators and received multiplayer markers while the full map is closed.
- Uses a local Unity canvas without changing matchmaking or replicated game state.

Road and trail detail comes from the normal map's imagery. The mod does not generate additional routes or reveal unexplored areas. Matching the full map's appearance remains a work in progress.

## Requirements

- Windows, 64-bit version of **over the hill**.
- MelonLoader **0.7.3**, configured for the game's IL2CPP build. https://github.com/lavagang/MelonLoader/releases
- The .NET runtime required by MelonLoader; this build was compiled against .NET **6.0.36**.

Developed against game version **0.0.1**, Unity **6000.0.75f1**. Compatibility with other game or loader versions is unverified.

## Installation

1. Close the game.
2. Install MelonLoader if it is not already installed, then launch and close the game once so it generates its assemblies.
3. Copy `OverTheHill.Minimap.dll` into the game's `Mods` folder:

   ```text
   over the hill/
   ├── over the hill.exe
   ├── MelonLoader/
   └── Mods/
       └── OverTheHill.Minimap.dll
   ```

4. Launch the game and load into a drive.
5. Open and close the normal map once to initialize its icon UI.

Steam's **Manage → Browse local files** opens the game folder. If building from source, the DLL is generated in `bin/Release/net6.0/`.

## Controls

| Key | Action |
| --- | --- |
| **F6** | Show or hide the minimap |
| **Page Up** | Zoom in |
| **Page Down** | Zoom out |

Place or change markers through the normal map. The minimap is a display overlay and does not accept clicks or place markers itself.

## Live updates

Vehicle following and copied graphic positions update every frame. After the normal map initializes, its player, moving-object, POI, and shared-pin displays refresh every **0.1 seconds** while the full map is closed.

“Live updates” refers to the map display during gameplay. The mod does not download or install new releases automatically.

## Configuration

After the first launch, look for the `Minimap` category in MelonLoader's preferences under the game's `UserData` folder. Close the game before manually editing preferences.

| Setting | Default | Purpose |
| --- | --- | --- |
| `Enabled` | `true` | Show the minimap |
| `Size` | `260` | Map width and height in pixels at 1080p; scales with screen height |
| `WorldSpan` | `1000` | Width of the visible area in world units; smaller values zoom in |

Toggle and zoom changes made with the keyboard are saved automatically.

## Multiplayer compatibility

The minimap is designed to run on your client only. It contains no Harmony patches, RPC calls, lobby changes, or replicated-state modifications. Other players should not need the minimap installed.

**Joining unmodded lobbies and live multiplayer marker behavior have not yet been verified.** Other installed mods may have their own multiplayer requirements.

## Known limitations and troubleshooting

| Issue | What to check |
| --- | --- |
| Icons are missing | Open and close the normal map once after loading. Icon initialization depends on the game's map UI. |
| Position, icons, or roads/trails look wrong | These remain under testing. Compare with the normal map at a similar zoom and include a screenshot in a bug report. |
| Minimap is hidden | Press **F6**, then confirm you are loaded into a drive with a local vehicle. |
| Mod does not load | Check the requirements and search `MelonLoader/Latest.log` for `Minimap`. |
| Game crashes | Close the game and remove `OverTheHill.Minimap.dll` to disable the mod. Include logs when reporting the crash. |

When reporting a bug, include the game version, mod version, installed mods, steps to reproduce, and relevant entries from `MelonLoader/Latest.log`. For alignment problems, include both the full map and minimap at the same location. Review logs for personal information before posting them publicly.

## Updating or uninstalling

**Update:** close the game and replace `Mods/OverTheHill.Minimap.dll` with the newer DLL. Keep only one copy of the minimap mod in `Mods`.

**Uninstall:** close the game and remove `Mods/OverTheHill.Minimap.dll`. Other mods can remain installed.

## Building from source

The current project uses local game assemblies and an installed runtime for an offline build.

Required development setup:

- .NET SDK **8**.
- .NET **6.0.36** runtime at `C:\Program Files\dotnet\shared\Microsoft.NETCore.App\6.0.36`, or adjusted reference paths in `Minimap.csproj`.
- A MelonLoader installation with generated `MelonLoader/Il2CppAssemblies` files.

Build from the repository root:

```powershell
dotnet build Minimap.csproj -c Release -p:GameDir="C:\Program Files (x86)\Steam\steamapps\common\over the hill"
```

Output:

```text
bin/Release/net6.0/OverTheHill.Minimap.dll
```

Game and MelonLoader assemblies are referenced locally and are not bundled in the build output.

## Changelog

### 0.2.1

- Corrected the 1024-unit fog-grid versus 512-pixel texture conversion.
- Uses the native map coordinate conversion, including border padding, when available.
- Aligns map, fog, and copied graphics through a shared map rectangle.
- Removes dependence on shared screen transforms when positioning copied graphics.
- Adds alignment and graphic-type diagnostics to the log.

### 0.2.0

- Added mirroring of normal-map image graphics and live display refreshes for players, POIs, and multiplayer pins.

### 0.1.1

- Replaced IMGUI texture rendering with Unity canvas rendering after a crash was traced to the original drawing path.

### 0.1.0

- Initial minimap prototype.

## Project status

The latest Release build compiles with zero warnings and zero errors. That confirms compilation only; visual accuracy and multiplayer compatibility require in-game testing.

This is an unofficial community mod and is not affiliated with or endorsed by the game's developers.
