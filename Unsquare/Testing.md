# In-game test protocol

How to verify Unsquare in a running city with Unsquare mode (mouse tool, hotkeys, cage overlay).

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
   `Mods\Unsquare` must also contain `Unsquare.mjs`, `Unsquare.css` and `images\` (toolbar button, tool window).
4. Load a city, turn on Unsquare mode (button next to the Underground toggle, or Alt+U) and click a building: it
   becomes the edit target. A building selected in the info panel before entering the mode is taken over.
5. After an update that bumps the manifest format (e.g. to 0.6.0), start a new city: older deformed buildings
   are discarded (log `Variant manifest has an old format …`).

## Mouse (in Unsquare mode)

| Action | Input |
|---|---|
| Preview | Hovering a building shows its cage dimmed (while no handle is selected) |
| 1. Select a building | Click it (outside the current cage); no handle is selected yet |
| 2. Select a handle | Click an edge, face or corner, depending on the pick mode (default edges; Alt+W cycles) |
| Move the selection | Drag an arrow of the gizmo (X red, Y green, Z blue), or the yellow square to move it horizontally |
| Move a handle sideways | Press on an edge, face or corner and drag: it is selected and moves horizontally (height kept); a click without moving only selects |
| Step back | Right-click: cancels a drag, else clears the handle, else the building; never leaves the mode |

Clicks close to the selected building's cage never change the selection. A click far away (outside the cage grown by
the far-click margin, default 5 % of its size) steps back: first the handle is deselected, then the building. While a
handle is selected the building is locked: clicks on other buildings do nothing. Only Esc, Alt+U and the toolbar button
leave Unsquare mode.

## Tool window (in Unsquare mode)

While a building is selected, a small window floats above its roof: pick corners / edges / faces (the active one is
highlighted), reset the selected corner, edge or face (disabled without a selection), reset the whole building and
lift the terrain with the building (highlighted while on, disabled while undeformed) and fit the bottom corners to
the original ground. The buttons run the same commands as Alt+1/2/3, Alt+R, Alt+Backspace, Alt+T and Alt+G.
Then come the align buttons (left, back, right as seen from the street; Alt+Shift+arrows) and the align mode button
(angle → flush → full; Alt+A).

## Hotkeys (defaults; all except Alt+U only work in Unsquare mode)

| Action | Key |
|---|---|
| Unsquare mode on / off | Alt+U |
| Leave Unsquare mode (only in the mode) | Esc |
| Cycle pick mode edges → faces → corners | Alt+W |
| Pick corners / edges / faces | Alt+1 / Alt+2 / Alt+3 |
| Select previous / next handle of the pick mode | Alt+Q / Alt+E |
| Move −X / +X (width) | Alt+← / Alt+→ |
| Move +Z / −Z (+Z = street side) | Alt+↑ / Alt+↓ |
| Move up / down | Alt+PageUp / Alt+PageDown |
| Cycle step 0.1 / 0.5 / 2 m | Alt+S |
| Reset selected corner / edge / face | Alt+R |
| Reset selected / reset all | Alt+Backspace / Alt+Shift+Delete |
| Terrain lift on / off for the selected building | Alt+T |
| Fit back corners to the original ground (street side to road level) / all corners | Alt+G / Alt+Shift+G |
| Align left / back / right side (seen from the street) to its neighbour | Alt+Shift+← / ↑ / → |
| Cycle align mode (angle → flush → full) | Alt+A |
| Undo / redo cage edit (nudge, whole drag, reset; all buildings) | Ctrl+Z / Ctrl+Y |
| Write cage state to the log | Alt+L |

Overlay: dashed = original box; solid cage colour = status (white undeformed, yellow pending/applying, green
applied, red failed, magenta = lot outline leaves its plot); orange = selected handle (thick edge, face outline and
dot, or corner dot), cyan = hovered handle of the pick mode; gizmo (only with a selected handle) on it with arrows along the building axes X (red),
Y (green), Z (blue) and a yellow square between X and Z for horizontal moves; the hovered or dragged part is brighter and thicker.

## Checklist

| # | Test | Expected |
|---|---|---|
| 1 | Select a zoned building, rotate the camera | Box and cage line up with the building at any building rotation |
| 2 | Alt+1/2/3, Alt+Q/E | The selection visits every corner, edge and face; gizmo arrows match the move keys |
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
| 29 | Click the Unsquare button, then Alt+U, then the button again | Button highlighted while the mode is on; log `[Edit] Unsquare mode on/off` each time |
| 30 | Mode off: press Alt+→, Alt+Shift+Delete, click buildings | Nothing happens (no `[Edit]` log, no overlay); buildings open the info panel as usual |
| 31 | Mode on: click a house, then another house, then empty ground | Cage moves to the second house; empty ground changes nothing; log `[Edit] Target …` per switch |
| 32 | Hover the cage in each pick mode (Alt+W) | Edge mode: edges turn cyan near the mouse; face mode: faces; corner mode: corners; log `[Edit] Pick mode …` |
| 33 | Click an edge, then a face (Alt+W), then a corner | First click of a house selects nothing; each later click moves the orange selection and gizmo; log `[Edit] Selected …` |
| 34 | Select the top face, drag the green arrow up | Roof follows the mouse; cage yellow while dragging, rebake after release; one `Drag of Face3 ended -> …` line |
| 35 | Select a front edge, drag the red and blue arrows | Both corners of the edge move together along X / Z |
| 36 | Start a drag, right-click while holding the mouse | Shape snaps back to before the drag; log `Drag … cancelled` |
| 37 | With a handle selected, click a neighbour next to the cage; then right-click and click the neighbour | Locked: nothing changes; after the right-click (log `Selection cleared`) the neighbour becomes the target |
| 38 | Right-click repeatedly | Selection cleared, then target cleared (cage gone), then nothing more; the mode stays on |
| 39 | Select an edge and click just beside it (on the ground or a neighbour) | Cage and gizmo stay; nothing changes |
| 40 | Zoom far out and close in with a selection | Gizmo keeps its screen size (never shorter than the min gizmo length); arrows stay grabbable |
| 41 | Look straight down the Y arrow and drag it | No jumps: frames where the ray runs along the arrow are skipped |
| 42 | Options → Advanced: change pick radius, far-click margin and gizmo size | Picking gets easier/harder, far clicks need more/less distance, gizmo resizes immediately |
| 43 | Leave the mode during a drag (Esc or Alt+U) | Drag cancelled, cage and gizmo hidden |
| 44 | Mode on, no building selected: move the mouse over several houses and the ground | Each house shows a dim cage while hovered; none over the ground |
| 45 | Select a house (no handle): hover other houses; then select an edge and hover them again | Other houses are previewed; with the edge selected no preview appears |
| 46 | With an edge selected, click ~1 m beside the cage, then a few metres away, then again far away on the ground | Near click: nothing; far click: `Selection cleared` (cage stays); second far click: `Target cleared` (cage gone) |
| 47 | Options → Advanced → Reset advanced options, select a handle | Gizmo about half its previous size (6 % of the view height, at least 1 m) |
| 48 | Press Esc in the mode (also during a drag); press Esc outside the mode | Mode off (`Unsquare mode off`, button not highlighted, drag cancelled); outside the mode Esc behaves as usual. Note whether the pause menu opens too |
| 49 | Select a face, Alt+PageUp ×3, Ctrl+Z ×3, Ctrl+Y ×1 | Each undo takes back one nudge (`[Edit] Undo on …`), redo repeats the first; selection stays |
| 50 | Drag an arrow over several metres, Ctrl+Z | The whole drag is undone in one step |
| 51 | Deform house A, select house B (lock it on a handle), Ctrl+Z | House A is reverted and becomes the target; B's selection is dropped |
| 52 | Deform house A, then house B, bulldoze B, Ctrl+Z | B's edit is skipped silently; A's edit is undone |
| 53 | Ctrl+Z after Alt+Shift+Delete or after loading another save | `[Edit] Nothing to undo` |
| 54 | Mode off: press Ctrl+Z / Ctrl+Y | Nothing happens (no `[Edit]` log) |
| 55 | Raise the top face, select one of its corners (Alt+1), Alt+R | Only that corner drops back; the other three stay raised; log `Reset Corner… -> Accepted` |
| 56 | Building selected without a handle, Alt+R | Log `No handle selected`; nothing changes |
| 57 | After a handle reset, Ctrl+Z | The corner/edge/face returns to where it was in one step |
| 58 | Enter the mode without a building, then click one | No window at first; then it appears centred above the roof |
| 59 | Pan, rotate and zoom the camera; raise one top corner | The window follows the roof and stays above the highest corner; at the screen border it stops at the edge; with the building behind the camera it hides |
| 60 | Click the corner / edge / face buttons, then press Alt+1/2/3 | Pick mode switches (`[Edit] Pick mode …`); the highlighted button always matches the mode |
| 61 | Window: reset-part button without and with a selected handle | Greyed out without a handle; with one it resets only that handle (`Reset …`), Ctrl+Z restores it |
| 62 | Window: reset-building button on a deformed building | Original shape (`[Edit] Reset -> …`); undoable with Ctrl+Z |
| 63 | Click a window button while a handle is selected | Selection and target stay; the click reaches neither the building nor the ground |
| 64 | Right-click until the target is cleared, then leave the mode | The window hides with the target and stays hidden outside the mode |
| 65 | Raise the front bottom edge of a building by ~10 m | Ground rises from back to front across the whole lot, no ridge at the lot border; log `Terrain slope for prefab …` |
| 66 | Raise only one bottom corner by 4 m | Ground slopes up towards that corner; the other corners stay at ground level |
| 67 | Raise the whole bottom face by 3 m | The ground under the whole lot rises evenly by 3 m |
| 68 | Lower a bottom corner by 2 m | Ground sinks towards that corner |
| 69 | Reset a lifted building; save and reload a lifted one | Flat again after reset; the slope comes back after the reload |
| 70 | Next to a sloped building, build or remove a road | The slope stays; the lot edge still blends to the road |
| 71 | Deform a building, then press Alt+T (or the terrain button) twice | Button highlighted after the deform; first press flattens the lot (`[Edit] Terrain lift off`, bake with 0 new assets), second lifts it again |
| 72 | Two buildings with the same deformation, terrain lift off on one; save and reload | Only that one stays flat, also after the reload; Ctrl+Z / Ctrl+Y step through the toggles |
| 73 | Alt+T on an undeformed building | Button greyed out; log `[Edit] Terrain lift on -> Rejected` |
| 74 | On a slope falling away from the road, select a placed building and press Alt+G (or the fit button) | A frame later the back corners sit on the original slope and the street side at road level, building height unchanged; log `[Fit] Building …: original ground at the corners …` and `[Edit] Fit to ground on … -> Accepted` |
| 75 | Compare the `[Fit]` corner heights with the visible slope (e.g. a corner on a hillside) | Higher corners get the larger values; the fitted corners touch the natural ground |
| 76 | After a fit, Ctrl+Z | The shape before the fit returns in one step |
| 77 | Fit with terrain lift on, then Alt+T | Lifted: the lot follows the slope; flat: the pad stays level |
| 78 | Alt+Shift+G on the same building; then raise its front corners and press Alt+G | All four corners follow the original ground (`Fit to ground (all corners)`); Alt+G brings the street side back to road level |
| 79 | Mode on: hover and click a park's grass, raise a top edge, pull a bottom corner out | Park previewed and selected from its lot; cage about 8 m tall, vertical edges easy to pick; benches, trees and paths follow the cage; no `has no lot cage` log |
| 80 | Click a house's garden (away from the house mesh) | The house is selected |
| 81 | Freshly placed park with curved paths: deform it twice, then Reset | Paths stay connected and bend smoothly, the road entrance stays attached; after Reset the layout equals a fresh park; `Lot sync … N net edges moved` without unmapped parts |
| 82 | Park at a road: drag a street-side corner or edge sideways, then Reset | Path ends slide along the sidewalk and stay connected (pedestrians still walk in); Reset brings them back; log `… M road ends slid`, no warnings |
| 83 | Edge pick mode: press on an unselected vertical edge and drag | Edge is selected and follows the mouse sideways; its height never changes; one Ctrl+Z restores it |
| 84 | Click an edge without moving the mouse | Only selects it; no drag log, nothing to undo |
| 85 | With a handle selected, hover and drag the yellow gizmo square; right-click mid-drag | Square brighter on hover; handle moves in the horizontal plane; right-click snaps back |
| 86 | Repeat 83 with a corner and a face (Alt+W), also at a flat camera angle | Same horizontal move; no jumps when the view is nearly level |
| 87 | Options → Advanced: raise the drag threshold to 20 px | Small mouse jitter on a press no longer drags |
| 88 | Building next to a turned neighbour lot, angle mode: Alt+Shift+← (or the left align button) | The left side (seen from the street) turns parallel to the neighbour's edge; the corner nearer the neighbour stays, the other slides along its front or back edge; log `Aligning Left of building … to lot …` |
| 89 | Alt+A to flush, Ctrl+Z, align again | Tool window icon changes; the side turns parallel and moves onto the neighbour's edge (gap closed, depth line kept) |
| 90 | Alt+A to full, align toward a lot | The side's corners sit on the neighbour lot's corners (shared edge), even if the side gets longer or shorter |
| 91 | Back of a lot near a road (any mode), Alt+Shift+↑ | Back face turns to the road edge (sidewalk outline), full behaves like flush |
| 92 | Align a side with nothing within the align reach | Nothing changes; log `[Edit] Align … -> Rejected` |
| 93 | Undo / redo after each align | One step each; the target building stays selected |

## Reporting problems

Note the checklist number, press Alt+L on the affected building and attach the log lines starting at the last
`[Edit]` command (include any `Bake`, `Restored` or error lines).

Known risks to watch: Alt+arrow keys may also pan the camera (rebind if so); baking fails with "not CPU readable" if
render meshes cannot be read; if buildings come back undeformed after reload, variant restore at preload did not run.
