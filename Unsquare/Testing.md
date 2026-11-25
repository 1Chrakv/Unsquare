# In-game testing

How to verify Unsquare in a running game. What the mod must do is listed in [Requirements.md](Requirements.md), each
requirement with a stable ID. Most are checked by the automated suite in `IngameTests/`; the rest by Claude
(review packs and charters). This file covers setup, running both, and the controls for testing by hand.

## Setup

1. **Close the game**, then `dotnet build Unsquare.sln` (Debug): the toolchain replaces `Mods\Unsquare` with the
   build. While the game runs the DLLs are locked: the deploy fails *and* leaves `Mods\Unsquare` half-deleted.
2. Start the game, open Options → Unsquare → Key bindings and check the bindings (rebind any that clash).
3. Follow the log live:

   ```powershell
   Get-Content -Wait "$env:USERPROFILE\AppData\LocalLow\Colossal Order\Cities Skylines II\Logs\Unsquare.Mod.log"
   ```

   The first line, e.g. `Unsquare 0.1.0+abc1234 (Debug, built 2026-10-08 20:05:21 UTC) loading`, must show the
   commit and build time you expect; otherwise the game runs an older build (ENV-001).
4. Load a city, turn on Unsquare mode (button next to the Underground toggle, or Alt+U) and click a building: it
   becomes the edit target. A building selected in the info panel before entering the mode is taken over.

## Automated tests

The suite drives the game through the automation bridge, a REST endpoint inside Debug builds of the mod
(`Unsquare/Automation/`, off unless enabled). See `IngameTests/README.md` for details.

1. Once: build the test city from `IngameTests/Fixture.md` and save it as `UnsquareTest-Fixture`; `npm install` in
   `IngameTests/`.
2. Enable the bridge (creates `ModsData\Unsquare\automation\enabled`), deploy a Debug build and start the game:

   ```powershell
   cd IngameTests; npm run bridge:enable
   ```

3. Run the tests (the game must be running; tests load the fixture themselves):

   ```powershell
   npm test
   ```

   - `npm run test:slow`: tests that restart the game, move files or simulate days.
   - `npm test -- -t UNDO-`: one requirement group.
   - `npm run fixture:check`: what the fixture city is missing.
   - `npm run coverage:req`: which requirements have tests, and that every old checklist row is mapped.
4. Results are in `.testruns\<timestamp>\report.md` (requirement ID → pass, fail or skip, with log excerpts).
   Tests flagged `needs-mod` or `needs-save` are skipped when their mod or save is missing.

## Claude's part

Requirements the suite can't judge alone (Check `suite+review:<pack>` or `claude:<charter>` in Requirements.md):

- `/ingame-review`: Claude judges the evidence packs of the last run (screenshots, probes, log) and records a
  verdict per requirement. Unchanged packs reuse their last verdict.
- `/ingame-explore <charter>`: Claude drives the game along a charter (options page, upgrade naming, placement tools,
  missing manifest).
- `/ingame-triage <test>`: Claude replays a failing test step by step and narrows down the cause.

Claude reaches the game through the `unsquare-game` MCP server (`IngameTests/mcp/`), the same bridge the suite
uses.
## Mouse (in Unsquare mode)

| Action | Input |
|---|---|
| Preview | Hovering a building shows its cage dimmed (while no handle is selected) |
| 1. Select a building | Click it (outside the current cage); no handle is selected yet |
| 2. Select a handle | Click an edge, face or corner, or anywhere on the cage in cage mode, depending on the pick mode (default edges; Alt+W cycles) |
| Move the selection | Drag an arrow of the gizmo (X red, Y green, Z blue), or the yellow square to move it horizontally |
| Rotate the selection | Tap Ctrl alone (or the Move/Rotate toggle in the tool window), then drag a ring (around X red, Y green, Z blue); edges get the two rings they can turn around, faces all three, corners keep the arrows |
| Move a handle sideways | Press on an edge, face or corner and drag: it is selected and moves horizontally (height kept); a click without moving only selects |
| Step back | Right-click: cancels a drag, else clears the handle, else the building; never leaves the mode |

Clicks close to the selected building's cage never change the selection. A click far away (outside the cage grown by
the far-click margin, default 5 % of its size) steps back: first the handle is deselected, then the building. While a
handle is selected the building is locked: clicks on other buildings do nothing. Alt+U and the toolbar button leave Unsquare
mode; Esc is left to the game (its Back: default tool, then the pause menu).

## Tool window (in Unsquare mode)

While a building is selected, a small window floats above its roof, in five sections with a header each; the top
row holds only toggles, the bottom row only buttons:
- **Deform:** top: 3-way toggle Both / Building / Lot (Alt+D cycles), then the blend toggle None / All (Alt+B, None
  by default); bottom: Link, building follows lot (link, arrow to a lot, Alt+K) and lot follows building (link, arrow
  to a house, Alt+Shift+K). Link is disabled while the cages are equal; blend can be chosen before splitting. A hint
  line under the window says when the building is not on its lot's ground.
- **Pick:** top: 4-way toggle for corners / edges / faces / the whole cage (a highlight slides to the active one;
  Alt+1/2/3/4); bottom: gizmo toggle move / rotate (tap Ctrl).
- **Terrain:** top: 2-way toggle terrain follows the building / flat pad (dimmed while undeformed; Alt+T) and 2-way
  toggle for fitting back only / all corners (Alt+F); bottom: fit the bottom corners to the original ground (Alt+G).
- **Align to neighbour:** top: align mode toggle (angle / flush / full; clicking selects directly, Alt+A cycles;
  dimmed while levelling) and the position / height toggle; bottom: left, back, right and street side (as seen from
  the street), which align (Alt+Shift+arrows) or level with the neighbour's ground (Ctrl+Alt+arrows).
- **Reset:** top: the selected corner, edge or face (disabled without a selection; Alt+R) and the whole building
  (Alt+Backspace); bottom: undo and redo (disabled when there is nothing to undo or redo).

## Hotkeys (defaults; all except Alt+U only work in Unsquare mode)

| Action | Key |
|---|---|
| Unsquare mode on / off | Alt+U |
| Cycle edit mode both → building → lot | Alt+D |
| Link: the building follows the lot / the lot follows the building | Alt+K / Alt+Shift+K |
| Switch blending off / on | Alt+B |
| Cycle pick mode edges → faces → corners → cage | Alt+W |
| Pick corners / edges / faces / the whole cage | Alt+1 / Alt+2 / Alt+3 / Alt+4 |
| Select previous / next handle of the pick mode | Alt+Q / Alt+E |
| Move −X / +X (width) | Alt+← / Alt+→ |
| Move +Z / −Z (+Z = street side) | Alt+↑ / Alt+↓ |
| Move up / down | Alt+PageUp / Alt+PageDown |
| Cycle step 0.1 / 0.5 / 2 m | Alt+S |
| Reset selected corner / edge / face | Alt+R |
| Reset selected / reset all | Alt+Backspace / Alt+Shift+Delete |
| Terrain lift on / off for the selected building | Alt+T |
| Fit to the original ground / toggle back only (street side to road level) or all corners | Alt+G / Alt+F |
| Align left / back / right / street side (seen from the street) to its neighbour | Alt+Shift+← / ↑ / → / ↓ |
| Cycle align mode (angle → flush → full) | Alt+A |
| Level left / back / right / street side with its neighbour's ground | Ctrl+Alt+← / ↑ / → / ↓ |
| Undo / redo cage edit (nudge, whole drag, reset; all buildings) | Ctrl+Z / Ctrl+Y |
| Write cage state to the log | Alt+L |

Overlay: dashed = original box; solid cage colour = status (white undeformed, yellow pending/applying, green
applied, red failed, magenta = lot outline leaves its plot); orange = selected handle (thick edge, face outline and
dot, or corner dot), cyan = hovered handle of the pick mode; gizmo (only with a selected handle) on it with arrows along the building axes X (red),
Y (green), Z (blue) and a yellow square between X and Z for horizontal moves, or in Rotate mode rings around those axes
through the handle's centre; the hovered or dragged part is brighter and thicker.


## Reporting problems

Note the requirement ID, press Alt+L on the affected building and attach the log lines starting at the last
`[Edit]` command (include any `Bake`, `Restored` or error lines). For a failing automated test, attach its section
of `report.md`.

Known risks to watch: Alt+arrow keys may also pan the camera (rebind if so); baking fails with "not CPU readable" if
render meshes cannot be read; if buildings come back undeformed after reload, variant restore at preload did not run.