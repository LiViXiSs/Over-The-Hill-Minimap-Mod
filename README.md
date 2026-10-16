# Over the Hill Minimap

A client-side minimap mod for **over the hill**, built with MelonLoader. Keep a map in the corner of your screen while driving, with adjustable zoom and live updates.

**Current version: 0.4.7 — experimental.** Position alignment, icon visibility, roads/trails, and multiplayer markers are still being tested. This version includes fixes for previously reported alignment and missing-icon issues; those fixes have not yet been verified in-game.

## Features

- Minimap in the upper-right corner, centered on your vehicle, with optional heading-up rotation.
- Adjustable zoom and map size.
- Uses the game's map imagery and fog of war.
- Mirrors the normal map's icon sprites, colors, rotation, and visibility.
- Shows local and remote vehicle markers using the game-assigned multiplayer colors. Your marker has a white outline.
- Updates player positions/headings every frame and reconciles joins, leaves, and colors every 0.5 seconds.
- Refreshes received multiplayer pins while the full map is closed.
- Uses a local Unity canvas without changing matchmaking or replicated game state.

Road and trail detail comes from the normal map's imagery. The mod does not generate additional routes or reveal unexplored areas. Matching the full map's appearance remains a work in progress.

## Requirements

- Windows, 64-bit version of **over the hill**.
- MelonLoader **0.7.3**, configured for the game's IL2CPP build.
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
| **F7** or **Rotate: ON/OFF** button | Toggle player-following rotation |
| **Page Up** | Zoom in |
| **Page Down** | Zoom out |

All four controls are also clickable buttons below the minimap. The controls stay visible when F6 hides the map. Place or change markers through the normal map; the minimap does not place markers.

## Live updates

Vehicle following and copied graphic positions update every frame. After the normal map initializes, its player, moving-object, POI, and shared-pin displays refresh every **0.1 seconds** while the full map is closed.

“Live updates” refers to the map display during gameplay. The mod does not download or install new releases automatically.

## Configuration

After the first launch, look for the `Minimap` category in MelonLoader's preferences under the game's `UserData` folder. Close the game before manually editing preferences.

| Setting | Default | Purpose |
| --- | --- | --- |
| `Enabled` | `true` | Show the minimap |
| `RotateWithPlayer` | `true` | Keep your heading at the top; disable for north-up |
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

### 0.4.7

- Removes obsolete native-icon references from POI diagnostic logging.
- Fixes the reported NullReferenceException in RectTransform.get_anchoredPosition when a cached native map icon has been destroyed.
- Keeps the working world projection, discovery updates, and compact controls.
- Version 0.4.6 marker alignment was confirmed working in user testing; this logging fix awaits runtime verification.


### 0.4.6

- Removes the POI update early return when the full map or its icon dictionary is unavailable.
- Cached POIs continue reprojecting onto the rendered terrain every frame, including zoom changes after closing the full map.
- Falls back to the same live fog-grid projection used by the basic map when the native map view is unavailable.
- Runtime verification of the closed-map zoom case is pending.


### 0.4.5

- Positions POI centers directly on the rendered map quad using its actual local rectangle, scale, rotation, and origin.
- Logs map geometry and nearby marker positions on zoom changes to diagnose remaining terrain/marker drift.
- This is a diagnostic build; the reported zoom-alignment bug is not yet confirmed resolved.


### 0.4.4

- Uses POI world positions through the native WorldToUIPosition conversion every frame, matching MapView.UpdateObjectives in the installed game build.
- Removes POI position sampling/caching from the full map UI, which can change with layout and zoom.
- Retains sprite caching, discovery updates, and compact controls.
- Native placement was inspected at RVA 0x9F43C9–0x9F442C and 0x9F44B3–0x9F452E; visual regression verification is pending.


### 0.4.3

- Converts POI positions through the full icon-to-map transform hierarchy rather than treating parent-relative anchored positions as map coordinates.
- Caches normalized positions while the full map is closed and retains markers through temporary native UI dictionary/sprite rebuilds.
- Reduces the controls footer from four rows to two compact rows, with smaller buttons and text.
- Position stability and visibility still require an in-game regression check.


### 0.4.2

- Uses native map-icon anchored positions for POIs instead of logical POI object transforms.
- Logs the number of POIs within the current map crop to diagnose missing markers.
- Adds clickable Map (F6), Rotate (F7), Zoom in (Page Up), and Zoom out (Page Down) controls below the map.
- Keeps the controls visible when the map is hidden so it can be restored by clicking.


### 0.4.1

- Added dedicated discovered-POI markers using native icon sprites and POI world positions.
- Uses fixed-size glyphs and the default UI material so closed-map layout, zoom, and shaders cannot shrink or hide POI icons.
- Refreshes discovery state and sprite changes every 0.2 seconds; keeps icons upright when the map rotates.
- Adds a diagnostic count of known POIs and discovered entries with sprites.
- In-game POI visibility verification is pending.


### 0.4.0

- Replaced the player indicator with a heading marker tinted by the game-assigned player color.
- Added live markers for remote player vehicles already replicated to your client.
- Added join/leave cleanup and color refresh without requiring the full map to be open.
- Suppressed duplicate normal-map player graphics while retaining shared pins and POIs.
- Multiplayer color/position behavior requires an in-game session check. Players outside the current minimap view are clipped; no off-screen arrows are added.


### 0.3.0

- Added saved heading-up rotation with a clickable toggle and F7 shortcut.
- Keeps the map frame fixed while rotating map imagery, fog, and markers together.
- Refreshes already-discovered fog pixels and advances native POI discovery scans while the full map is closed.
- Uses current map/fog textures when the game replaces them.


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









