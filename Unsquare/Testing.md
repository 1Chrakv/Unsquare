# In-game test protocol

How to verify Unsquare in a running city with the hotkeys and the cage overlay (the first parts of the final UI).

## Setup

1. **Close the game**, then `dotnet build Unsquare.sln` – the toolchain replaces `Mods\Unsquare` with the build.
   While the game runs the DLLs are locked: the deploy fails *and* leaves `Mods\Unsquare` half-deleted.
2. Start the game, open Options → Unsquare and check the key bindings (rebind any that clash).
3. Follow the log live:

   ```powershell
   Get-Content -Wait "$env:USERPROFILE\AppData\LocalLow\Colossal Order\Cities Skylines II\Logs\Unsquare.Mod.log"
   ```

   The first line, e.g. `Unsquare 0.1.0+abc1234 (Debug, built 2026-10-08 20:05:21 UTC) loading`, must show the
   commit and build time you expect; otherwise the game runs an older build.
4. Load a city and click a building: the info panel selection is the edit target.
5. After an update that bumps the manifest format (e.g. to 0.6.0), start a new city: older deformed buildings
   are discarded (log `Variant manifest has an old format …`).

## Hotkeys (defaults)

| Action | Key |
|---|---|
| Handle kind corner / edge / face | Alt+1 / Alt+2 / Alt+3 |
| Previous / next handle | Alt+Q / Alt+E |
| Move −X / +X (width) | Alt+← / Alt+→ |
| Move +Z / −Z (+Z = street side) | Alt+↑ / Alt+↓ |
| Move up / down | Alt+PageUp / Alt+PageDown |
| Cycle step 0.1 / 0.5 / 2 m | Alt+S |
| Reset selected / reset all | Alt+Backspace / Alt+Shift+Delete |
| Write cage state to the log | Alt+L |

Overlay: dashed = original box; solid cage colour = status (white undeformed, yellow pending/applying, green
applied, red failed, magenta = lot outline leaves its plot); orange dot = selected handle with building axes
X (red), Y (green), Z (blue).

## Checklist

| # | Test | Expected |
|---|---|---|
| 1 | Select a zoned building, rotate the camera | Box and cage line up with the building at any building rotation |
| 2 | Alt+1/2/3, Alt+Q/E | Orange dot visits every corner, edge midpoint and face centre; axes match the move keys |
| 3 | Alt+3, select the top face (Face 3), Alt+PageUp ×4 | Cage turns yellow; ~0.4 s after the last press the building rises and the cage turns green; one `Bake …` log line |
| 4 | Zoom far out | Shape stays deformed (LODs deformed too) |
| 5 | Move single corners and edges | Taper, skew and twist look smooth; lighting/normals look right |
| 6 | Push a face through the opposite face | Log `-> Rejected`, shape unchanged |
| 7 | Push a handle far beyond the box | Log `-> Clamped` |
| 8 | Info panel, then run a few in-game days | Residents/jobs/level unchanged; no condition or abandonment problems |
| 9 | Same offsets on a second building of the same type | `Bake …` shows `0 new assets` |
| 10 | Save, quit to menu, reload; then restart the game and load | Still deformed; log `Restored N variants, 0 failed` |
| 11 | Look into `…\Cities Skylines II\ModsData\Unsquare\` | `variants.json`, `bounds.json`, `UQ_*.Geometry` files |
| 12 | Alt+Backspace, then Alt+Shift+Delete | Original shape, white cage; reset-all logs a count |
| 13 | Zone a large area with the base building's zone | No deformed variant ever spawns |
| 14 | Reset a building, save, set its `LastUsedUtc` in `variants.json` > 14 days back, restart | Housekeeping log: entry pruned, its assets deleted, shared assets kept |
| 15 | Delete `variants.json`, load a save with deformed buildings | Document what breaks (see `HowTo.md` §13.7) |
| 16 | On a building with roof props and a storefront: raise the top face, lean it sideways, pull a bottom front corner out | Roof props rise, move and tilt with the roof; the storefront follows its corner and stays on the ground; reset puts all props back |
| 17 | House with garden (shed, trees, paving, fences): skew/taper the lot inside its plot | Cage spans the fence rectangle; `Attachments of …` log lists props/areas/lanes/nets; paving, grass, fences, paths, shed and trees follow; trees stay upright; `Lot sync …` log shows moved areas/net edges |
| 18 | Push a lot bottom corner outside the plot | Cage turns magenta, log warns once; the edit still applies |
| 19 | Save, reload, cold restart | Deformed buildings restore; lot areas/paths stay deformed |
| 20 | Reset a lot-deformed building | House, props, fences, areas and paths return to the original layout |
| 21 | Building with a storefront: pull a bottom front corner in | Storefront turns with the facade and stays parallel to it; props deep inside the lot keep a smooth average turn; roof props near the eaves follow the roof |
| 22 | Options → Unsquare → Advanced | All groups shown with defaults; every change logs `Options: …` once |
| 23 | Facade alignment reach 0, deform a new building with a storefront | Storefront takes the average turn, not the facade's |
| 24 | Turn off "Deform ground areas", lot-deform a fresh house | Paving and grass stay rectangular; `Attachments of …` log shows `Areas off` |
| 25 | Change nudge step sizes, press Alt+S repeatedly | Steps cycle through the new sizes (snapped to 0.1 m) |
| 26 | Change line thickness / handle sizes / axis length | Overlay updates immediately |
| 27 | Reset advanced options, restart the game | Defaults restored and kept after the restart |
| 28 | Deform a building, select another one, reselect it and nudge again | The cage shows the current shape; the nudge continues from it (never from undeformed) |

## Reporting problems

Note the checklist number, press Alt+L on the affected building and attach the log lines starting at the last
`[Edit]` command (include any `Bake`, `Restored` or error lines).

Known risks to watch: Alt+arrow keys may also pan the camera (rebind if so); baking fails with "not CPU readable" if
render meshes cannot be read; if buildings come back undeformed after reload, variant restore at preload did not run.
