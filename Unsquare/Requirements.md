# Requirements

What Unsquare must do in a running game. Each requirement has a stable ID and is checked by the in-game test
suite (`IngameTests/`), with Claude stepping in only where the suite can't judge. `Testing.md` explains how to run
both; `IngameTests/Fixture.md` describes the test city.

**Rows** names the rows of the old manual checklist (up to 1.2) that the requirement replaces.

**Check**:

| Value | Meaning |
|---|---|
| `suite` | Automated test in the spec file of its group (`npm test`) |
| `suite-slow` | Automated, but restarts the game, moves files or simulates days (`npm run test:slow`) |
| `suite+review:<pack>` | The suite asserts the measurable part and writes an evidence pack that Claude judges (`/ingame-review`) |
| `claude:<charter>` | Claude drives the game along a charter (`/ingame-explore <charter>`) |

Flags after the check: `input` (needs synthetic mouse/keys, phase 3), `needs-mod:<mod>` and `needs-save:<kind>`
(skipped when the precondition is missing), `needs-build` (rebuilds the mod).

Rules: a requirement ID is never reused or renumbered; a removed requirement keeps its row with *(removed)*. New
features add requirements and tests in the same change.

## ENV – Test environment (`specs/env.spec.ts`)

| ID | Requirement | Rows | Check |
|---|---|---|---|
| ENV-001 | The running build is the one under test: the first log line names the expected commit and build time | Setup 3 | suite |
| ENV-002 | The deployed mod contains `Unsquare.mjs`, `Unsquare.css` and `images\` | Setup 3 | suite |
| ENV-003 | The automation bridge is Debug-only: a Release build contains no `Unsquare.Automation` types | – | suite |

## MODE – Unsquare mode (`specs/mode.spec.ts`)

| ID | Requirement | Rows | Check |
|---|---|---|---|
| MODE-001 | Alt+U, the toolbar button and the tool window trigger toggle the mode; the button is highlighted while it is on; each switch logs `[Edit] Unsquare mode on/off` | 29 | suite, input |
| MODE-002 | Outside the mode, edit hotkeys, Ctrl+Z/Ctrl+Y and clicks on buildings do nothing (no `[Edit]` log, no overlay); buildings open the info panel as usual | 30, 54 | suite, input |
| MODE-003 | Esc is left to the game: it first leaves the mode (`Unsquare mode off`), then opens the pause menu; it never steps back | 48 | suite, input |
| MODE-004 | Leaving the mode during a drag cancels the drag and hides cage and gizmo | 43 | suite |
| MODE-005 | A building selected in the info panel before entering the mode becomes the target | Setup 4 | suite |

## SEL – Selecting buildings and handles (`specs/selection.spec.ts`)

| ID | Requirement | Rows | Check |
|---|---|---|---|
| SEL-001 | Clicking a building makes it the target, clicking another switches the target, empty ground changes nothing; each switch logs `[Edit] Target …` | 31 | suite, input |
| SEL-002 | The first click on a building selects no handle; later clicks select a handle of the pick mode with gizmo; log `[Edit] Selected …` | 33 | suite, input |
| SEL-003 | With a handle selected the building is locked: clicks on other buildings do nothing; after a right-click (`Selection cleared`) another building can be picked | 37 | suite, input |
| SEL-004 | Right-click steps back: cancel the drag, else clear the handle, else the target, then nothing more; the mode stays on | 38 | suite |
| SEL-005 | Clicks close to the selected cage never change the selection; clicks outside the far-click margin step back (handle, then target) | 39, 46 | suite, input |
| SEL-006 | Hovering a building previews its cage while no handle is selected; no preview over the ground or while a handle is selected | 44, 45 | suite, input |
| SEL-007 | A park is picked from its lot; clicking a house's garden picks the house | 79, 80 | suite, input |
| SEL-008 | Hovering and clicking a stand-alone upgrade (school wing) picks the wing with its own cage; clicking the car park's storey picks the car park; the school or its lot picks the school | 117, 118 | suite, input |
| SEL-009 | Reselecting a deformed building shows its current shape and nudges continue from it, never from undeformed | 28 | suite |
| SEL-010 | Alt+L writes the cage state of the target to the log | Hotkeys | suite, input |

## CAGE – Cage geometry (`specs/cage.spec.ts`)

| ID | Requirement | Rows | Check |
|---|---|---|---|
| CAGE-001 | Rest box and cage line up with the building at any building rotation | 1 | suite |
| CAGE-002 | The cage spans the lot (fence) rectangle, at least 8 m tall so flat lots have pickable vertical edges | 17, 79 | suite |
| CAGE-003 | With a storey upgrade, cage and rest box reach the storey's top and the tool window sits above it; without upgrades the cage ends at the roof | 113, 115 | suite |

## PICK – Pick modes (`specs/pick.spec.ts`)

| ID | Requirement | Rows | Check |
|---|---|---|---|
| PICK-001 | Alt+1/2/3/4, Alt+W and the tool window switch the pick mode (corners, edges, faces, cage); log `[Edit] Pick mode …` | 32, 60 | suite |
| PICK-002 | Alt+Q/Alt+E visit every corner, edge and face of the pick mode | 2 | suite |
| PICK-003 | Hovering the cage highlights the nearest handle of the pick mode | 32 | suite, input |
| PICK-004 | In cage mode, hovering on or inside the cage highlights all edges; a click selects them all (`Selected Cage0`) with the gizmo at the cage centre | 153 | suite, input |

## GIZMO – Move and rotate gizmo (`specs/gizmo.spec.ts`)

| ID | Requirement | Rows | Check |
|---|---|---|---|
| GIZMO-001 | The gizmo arrows point along the building axes the move keys use (X width, Y up, Z street side) | 2 | suite |
| GIZMO-002 | The gizmo keeps its screen size when zooming and is never shorter than the min gizmo length; arrows stay grabbable | 40 | suite, input |
| GIZMO-003 | A Ctrl tap or the tool-window toggle switches Move/Rotate (log `[Edit] Gizmo mode Rotate`); both stay in sync; switching during a drag does nothing; Ctrl+Z/Y and Ctrl+Alt+arrows don't switch it | 145, 146, 152, 178 | suite, input |
| GIZMO-004 | In Rotate mode faces get three rings, edges the two rings they can turn around, corners keep the arrows | 149, 150 | suite |
| GIZMO-005 | After Reset settings the gizmo is 6 % of the view height, at least 1 m | 47 | suite |

## DRAG – Dragging (`specs/drag.spec.ts`)

| ID | Requirement | Rows | Check |
|---|---|---|---|
| DRAG-001 | Dragging the Y arrow of the top face raises the roof with the mouse; the cage is pending while dragging and rebakes right at release; one `Drag of Face3 ended -> …` line | 34 | suite |
| DRAG-002 | Dragging the X or Z arrow of an edge moves both its corners together along that axis | 35 | suite |
| DRAG-003 | A right-click during an arrow, square or ring drag snaps the shape back to before the drag; log `Drag … cancelled` | 36, 85, 151 | suite |
| DRAG-004 | Looking straight down an arrow and dragging it causes no jumps (frames with the ray along the axis are skipped) | 41 | suite, input |
| DRAG-005 | Pressing on a handle and dragging selects it and moves it horizontally with its height kept, also at a flat camera angle; one Ctrl+Z restores it; a click without moving only selects | 83, 84, 86 | suite, input |
| DRAG-006 | The yellow square moves the handle in the horizontal plane | 85 | suite |
| DRAG-007 | Dragging the top face's green ring twists the top around its centre; one Ctrl+Z undoes the whole turn | 147 | suite |
| DRAG-008 | Dragging the top face's red and blue rings slopes the roof around its centre without jumps, also when the rings are seen edge-on | 148 | suite, input |
| DRAG-009 | A turn that folds the cage shows the fold problem; the folding part is not applied: the building keeps the last shape of the drag that didn't fold | 151 | suite |
| DRAG-010 | Dragging the whole cage (arrow, square, or pressing on it) shifts the building, lot, paths and props together; one Ctrl+Z per drag | 154 | suite |
| DRAG-011 | Whole-cage drags follow the edit mode: Building moves only the building, Lot only ground items, Both everything | 157 | suite |
| DRAG-012 | The whole cage's green ring turns the building in place around its centre; red/blue rings tilt it | 155 | suite |
| DRAG-013 | Dragging the whole cage far away stops at the max corner offset (clamped) | 158 | suite |
| DRAG-014 | An autosave during a drag causes no hitch; the drag continues; the autosave holds the shape at the moment of saving | 190 | suite-slow |

## NUDGE – Hotkey nudges (`specs/nudge.spec.ts`)

| ID | Requirement | Rows | Check |
|---|---|---|---|
| NUDGE-001 | Nudging the top face up four times turns the cage pending; about 0.2 s after the last nudge the building rises and the cage is applied; one `Bake …` line | 3 | suite+review:overlay |
| NUDGE-002 | Alt+S cycles the step sizes; changed step options are used, snapped to 0.1 m | 25 | suite |
| NUDGE-003 | With the whole cage selected, Alt+arrows move the whole cage one step and Alt+R puts every corner back | 156 | suite |
| NUDGE-004 | Pushing a face through the opposite face is rejected (`-> Rejected`) and the shape is unchanged | 6 | suite |
| NUDGE-005 | Pushing a handle far beyond the box is clamped (`-> Clamped`) | 7 | suite |

## KEYS – Hotkeys (`specs/keys.spec.ts`)

| ID | Requirement | Rows | Check |
|---|---|---|---|
| KEYS-001 | Every default hotkey of the hotkey table runs its command in the mode | Hotkeys | suite, input |
| KEYS-002 | Key bindings can be rebound in Options; the new binding works and the old one doesn't | Setup 2 | suite, input |

## BAKE – Variants and baking (`specs/bake.spec.ts`)

| ID | Requirement | Rows | Check |
|---|---|---|---|
| BAKE-001 | The same offsets on a second building of the same type reuse the variant: `Bake …` shows `0 new assets` | 9 | suite |
| BAKE-002 | `ModsData\Unsquare` holds `bounds.json` and the `UQ_*.Geometry` assets of baked variants | 11 | suite |
| BAKE-003 | Taper, skew and twist look smooth; lighting and normals look right | 5 | suite+review:mesh |
| BAKE-004 | Bakes hold at most about 1,000,000 source vertices (`sources held N (M vertices)`); memory levels off over 15 large buildings | 213 | suite-slow |
| BAKE-005 | Repeated nudges of one building keep its bake time (row house about 30–40 ms, hospital 60–90 ms) | 214, 217 | suite |
| BAKE-006 | Plain buildings whose sources were released render at every LOD, without missing, pink or flickering meshes or asset errors | 215 | suite+review:mesh |
| BAKE-007 | Log `Released source meshes of N render prefabs (M vertices)` after a load that rebaked and when leaving the city | 216 | suite-slow |
| BAKE-008 | Zoning a large area with the base building's zone never spawns a deformed variant | 13 | suite-slow |

## LOD – Levels of detail (`specs/lod.spec.ts`)

| ID | Requirement | Rows | Check |
|---|---|---|---|
| LOD-001 | Far out the building stays deformed: every LOD mesh is deformed | 4 | suite |

## PROPS – Props and ground items (`specs/props.spec.ts`)

| ID | Requirement | Rows | Check |
|---|---|---|---|
| PROPS-001 | Roof props rise, move and tilt with the roof; a storefront follows its corner and stays on the ground; reset puts all props back | 16 | suite |
| PROPS-002 | Pulling a bottom front corner in turns a storefront with the facade, parallel to it; props deep inside the lot take a smooth average turn; roof props near the eaves follow the roof | 21 | suite |
| PROPS-003 | With facade alignment reach 0, a storefront takes the average turn, not the facade's | 23 | suite |
| PROPS-004 | Lot deformation moves paving, grass, fences, paths, sheds and trees along, trees upright; logs `Attachments of …` and `Lot sync …` | 17 | suite |
| PROPS-005 | With "Deform ground areas" off, paving and grass stay rectangular; `Attachments of …` shows `Areas off` | 24 | suite |

## LOT – Lot deformation (`specs/lot.spec.ts`)

| ID | Requirement | Rows | Check |
|---|---|---|---|
| LOT-001 | A lot bottom corner pushed outside the plot turns the cage magenta and warns once; the edit still applies | 18 | suite+review:overlay |
| LOT-002 | Resetting a lot-deformed building returns house, props, fences, areas and paths to the original layout | 20 | suite |
| LOT-003 | A park's cage is about 8 m tall; benches, trees and paths follow it; no `has no lot cage` log | 79 | suite |
| LOT-004 | Curved park paths stay connected and bend smoothly when deformed twice; the road entrance stays attached; after Reset the layout equals a fresh park; `Lot sync … N net edges moved` without unmapped parts | 81 | suite+review:ground |
| LOT-005 | Dragging a park's street side sideways slides path ends along the sidewalk, still connected; Reset brings them back; log `… M road ends slid`, no warnings | 82 | suite |

## TERRAIN – Terrain lift (`specs/terrain.spec.ts`)

| ID | Requirement | Rows | Check |
|---|---|---|---|
| TERRAIN-001 | Raising the front bottom edge by 10 m slopes the ground from back to front across the whole lot, without a ridge at the lot border; log `Terrain slope for prefab …` | 65 | suite |
| TERRAIN-002 | Raising one bottom corner by 4 m slopes the ground up towards it; the other corners stay at ground level | 66 | suite |
| TERRAIN-003 | Raising the whole bottom face by 3 m raises the ground under the whole lot evenly by 3 m | 67 | suite |
| TERRAIN-004 | Lowering a bottom corner by 2 m sinks the ground towards it | 68 | suite |
| TERRAIN-005 | Reset flattens the lot; after a reload the slope of a lifted building comes back | 69 | suite |
| TERRAIN-006 | Building or removing a road next to a sloped building keeps the slope; the lot edge still blends to the road | 70 | claude:placement |
| TERRAIN-007 | The terrain toggle is on after a deform; toggling flattens the lot (`[Edit] Terrain lift off`, bake with 0 new assets) and lifts it again | 71 | suite |
| TERRAIN-008 | Terrain lift is per building: of two buildings with the same deformation only the toggled one stays flat, also after a reload; Ctrl+Z/Y step through the toggles | 72 | suite |
| TERRAIN-009 | On an undeformed building the toggle is disabled and refused: `[Edit] Terrain lift on -> LotUndeformed` | 73 | suite |
| TERRAIN-010 | A lifted lot (pad > 5 m) looks the same after load, save + load, and restart + load; each load logs the same pad | 187 | suite-slow |

## FIT – Fit to ground (`specs/fit.spec.ts`)

| ID | Requirement | Rows | Check |
|---|---|---|---|
| FIT-001 | On a slope falling away from the road, Alt+G sets the back corners on the original slope and the street side at road level, building height unchanged; logs `[Fit] Building …` and `[Edit] Fit to ground on … -> Accepted` | 74 | suite |
| FIT-002 | The fitted corner heights equal the natural ground at each corner | 75 | suite |
| FIT-003 | One Ctrl+Z undoes a fit | 76 | suite |
| FIT-004 | After a fit, a lifted lot follows the slope and a flat one keeps a level pad | 77 | suite |
| FIT-005 | Alt+F or the toggle switches back-only/all-corners (`[Edit] Ground fit mode AllCorners`); all corners follow the original ground; back-only brings the street side back to road level; the mode stays when selecting another building | 78 | suite |
| FIT-006 | On a slope rising behind the lot, a back-only fit lifts the back corners onto the original ground; the street side stays at road level and the building keeps its height | – | suite |

## ALIGN – Align to neighbour (`specs/align.spec.ts`)

| ID | Requirement | Rows | Check |
|---|---|---|---|
| ALIGN-001 | Angle mode: the side turns parallel to the neighbour's edge, the corner nearer the neighbour stays, the other slides along its front or back edge; log `Aligning Left of building … to lot …` | 88 | suite |
| ALIGN-002 | Flush mode: the side turns parallel and moves onto the neighbour's edge (gap closed, depth line kept); Alt+A cycles the mode | 89 | suite |
| ALIGN-003 | Full mode: the side's corners sit on the neighbour lot's corners, even if the side gets longer or shorter | 90 | suite |
| ALIGN-004 | A back side near a road turns to the road edge; full behaves like flush there | 91 | suite |
| ALIGN-005 | With nothing within the align reach nothing changes; log `[Edit] Align … -> Rejected` | 92 | suite |
| ALIGN-006 | Each align is one undo/redo step; the target stays selected | 93 | suite |
| ALIGN-007 | A side dragged into the neighbour lot aligns back out onto the neighbour's facing edge, not its far side | 94 | suite |
| ALIGN-008 | A street side pulled back from the road aligns onto the road edge; log `Aligning Front of building … to road …` | 179 | suite |
| ALIGN-009 | A side beside a free-standing footpath turns parallel to it and moves onto its near edge; log `Aligning … to path …` | 184 | suite |
| ALIGN-010 | A path inside the lot is ignored (the side goes to the road beyond); the lot's own paths and neighbours' lot paths never count | 186 | suite |
| ALIGN-011 | In Building mode a side aligns to the neighbour's building cage (gardens stay); a park next door falls back to its lot edge | 135 | suite |

## LEVEL – Level with neighbour (`specs/level.spec.ts`)

| ID | Requirement | Rows | Check |
|---|---|---|---|
| LEVEL-001 | Ctrl+Alt+← moves the left side's two corner columns so their bottoms sit on the neighbour lot's ground (higher or lower); the columns keep their height and the right side stays; log `Levelling Left of building … with lot …` | 95 | suite |
| LEVEL-002 | The back side at a sloped road: each back corner goes to the road surface height next to it | 96 | suite |
| LEVEL-003 | Without a neighbour in reach levelling is rejected (`no neighbour within …`); a level is one undo step | 97 | suite |
| LEVEL-004 | The street side at a sloped street: both front bottom corners go to the road surface; the back stays | 180 | suite |
| LEVEL-005 | Levelling towards a free footpath moves that side's bottom corners to the path surface | 185 | suite |

## UNDO – Undo and redo (`specs/undo.spec.ts`)

| ID | Requirement | Rows | Check |
|---|---|---|---|
| UNDO-001 | Each undo takes back one nudge (`[Edit] Undo on …`), redo repeats it; the selection stays | 49 | suite |
| UNDO-002 | A whole drag is undone in one step | 50 | suite |
| UNDO-003 | Undoing an edit of another building reverts it and makes it the target; the current selection is dropped | 51 | suite |
| UNDO-004 | Edits of a bulldozed building are skipped silently; the next older edit is undone | 52 | suite, input |
| UNDO-005 | After reset-all or after loading another save there is nothing to undo (`[Edit] Nothing to undo`) | 53 | suite |
| UNDO-006 | A handle reset is undone in one step | 57 | suite |

## RESET – Resets (`specs/reset.spec.ts`)

| ID | Requirement | Rows | Check |
|---|---|---|---|
| RESET-001 | Alt+Backspace resets the building to its original shape (undeformed cage); Alt+Shift+Delete resets all and logs a count | 12 | suite |
| RESET-002 | Resetting a selected corner drops only that corner back; log `Reset Corner… -> Accepted` | 55 | suite |
| RESET-003 | Reset handle without a selected handle logs `No handle selected` and changes nothing | 56 | suite |
| RESET-004 | After Reset all, every building's props, areas and paths stand exactly where they stood before it was deformed, as after a single reset | – | suite |

## SPLIT – Edit modes and split cages (`specs/split.spec.ts`)

| ID | Requirement | Rows | Check |
|---|---|---|---|
| SPLIT-001 | Alt+D and the tool-window toggle cycle the edit mode Both → Building → Lot | Hotkeys | suite |
| SPLIT-002 | Both mode behaves as before split cages: lot box raised over storeys, no second cage, Link disabled | 127 | suite |
| SPLIT-003 | Building mode on a house with a garden: the cage hugs the building; a top-corner drag leans only the building; garden props, paths, grass and terrain stay; the lot cage shows as ghost | 128 | suite+review:overlay |
| SPLIT-004 | Widening the building 3 m over its garden keeps paths and lanes into the doors connected, bending them in a ring around the building; steps and bins move along; trees further out stay | 129 | suite+review:ground |
| SPLIT-005 | Lot mode: ground, paths, ground props and terrain follow a lot-corner drag; the building stays; the building cage shows as ghost | 130 | suite |
| SPLIT-006 | Raising the back of the lot shows the hint `The building is not on its ground`; Building-mode fit sets the building onto the lifted ground and the hint goes away; terrain toggle and fit mode are disabled in Building mode | 131 | suite |
| SPLIT-007 | A split building dragged by a lot corner in Both mode is carried along with its own lean; Link (Alt+K) makes it follow the lot exactly | 132 | suite |
| SPLIT-008 | Reset building resets only the building in Building mode, only the lot in Lot mode, everything in Both; each undo restores a step | 133 | suite |
| SPLIT-009 | Nudging the building cage up and back down by 0.1 m returns it exactly, without a leftover split; undo steps through every nudge | 134 | suite |
| SPLIT-010 | A lot-only deformation survives save, reload and restart; housekeeping evicts nothing it shouldn't (no rebake on the next load) | 136 | suite-slow |
| SPLIT-011 | Leaning a car park with a storey and a ground-level extension in Building mode leans the storey; the ground-level extension follows the lot | 137 | suite |
| SPLIT-012 | The last chosen edit mode is active again after a restart | 138 | suite-slow |
| SPLIT-013 | "Lot follows the building" (Alt+Shift+K) gives lot, paths and gardens the building's shape; the cages are linked again; Ctrl+Z restores the split | 142 | suite |
| SPLIT-014 | A hospital ward wing beside the main block is enclosed by the Building-mode cage and bends with the building; lot cage and paths stay; save and reload keep the shape | 143 | suite |
| SPLIT-015 | A storey is enclosed by the Building-mode cage; a flat helipad beside the building doesn't enlarge it and follows the lot cage | 144 | suite |
| SPLIT-016 | Buildings deformed before split cages keep their shapes and variant names; the manifest is rewritten in the current format | 126, Setup 5 | suite-slow, needs-save:1.1 |

## BLEND – Ground blending (`specs/blend.spec.ts`)

| ID | Requirement | Rows | Check |
|---|---|---|---|
| BLEND-001 | With blend None (default) a Building-mode wall move leaves everything on the ground in place | 139 | suite |
| BLEND-002 | With blend All, gardens next to the moved wall stay flush against it and the movement fades out; nothing bends into arcs or leaves the lot; benches and trees move with their paths and hedges | 140 | suite+review:ground |
| BLEND-003 | Ctrl+Z goes back to None; after a reload mode and shapes are kept | 141 | suite |

## UPGRADE – Upgrades and extensions (`specs/upgrade.spec.ts`)

| ID | Requirement | Rows | Check |
|---|---|---|---|
| UPGRADE-001 | Deforming a car park with the extra storey bends the storey with it, on its roof, ramps connected; logs `Upgrades of building …` (storey as extension) and `Lot sync … 1 extensions` | 106, 111 | suite |
| UPGRADE-002 | After a few minutes of simulation, Reset puts the storey back to its plain prefab and Ctrl+Z deforms it again, without a crash | 107, 111 | suite |
| UPGRADE-003 | The deformed storey survives save and reload; the car park's upgrade menu shows no extra entries | 108, 194 | suite |
| UPGRADE-004 | Deforming a school with its classroom wing and simulating a few minutes doesn't crash; Reset keeps the wing undeformed with connected paths | 109 | suite |
| UPGRADE-005 | The wing deformed alone doesn't crash (also when hovering wing and school); its paths stay connected; reset, undo and reload restore the shapes | 110, 119, 194 | suite |
| UPGRADE-006 | Hovering and clicking deformed buildings and their upgrades outside the mode, also paused, doesn't crash; log `Copied sim data to … variants (N archetypes completed)` | 112 | suite, input |
| UPGRADE-007 | Dragging and 0.1 m nudging a car park's top corner moves the storey's roof with it; undo and redo restore each step; Reset brings back building and storey | 114 | suite |
| UPGRADE-008 | After a reload the cage again reaches the storey's top | 116 | suite |
| UPGRADE-009 | A deformed school shows its normal name, the Upgrades section with every wing under its name, and named upkeep lines | 120 | claude:naming |
| UPGRADE-010 | Deformed hospital and car park list their deformed extensions under Upgrades | 121 | claude:naming |
| UPGRADE-011 | Upgrade menu, build menus, Find It and the milestone screen offer the usual entries and never a variant (`__uq`) or duplicate | 122 | claude:naming |
| UPGRADE-012 | Adding another storey to a deformed car park deforms it at once (`New upgrade on deformed building …`, then `Bake for building …`); hovering doesn't crash | 123 | claude:placement |
| UPGRADE-013 | A wing added to a deformed school is placed as usual and stays undeformed with its own cage | 124 | claude:placement |
| UPGRADE-014 | After switching the game language and back, deformed buildings keep their normal names | 125 | suite |

## LEVELUP – Level-ups (`specs/levelup.spec.ts`)

| ID | Requirement | Rows | Check |
|---|---|---|---|
| LEVELUP-001 | A deformed level-1 growable that levels up keeps the shape: same lot outline, lean scaled to the new height, paths on the deformed lot; log `levelled up: carrying its cage over` | 159 | suite-slow |
| LEVELUP-002 | The carried shape survives a reload; with *Keep shape on level-up* off, the next level-up comes back undeformed | 160 | suite-slow |
| LEVELUP-003 | A deformed growable saved while levelling up keeps its shape after reload when the level-up finishes | 193 | suite-slow |

## ZONING – Zoning, condemnation, simulation (`specs/zoning.spec.ts`)

| ID | Requirement | Rows | Check |
|---|---|---|---|
| ZONING-001 | Residents, jobs and level of a deformed building stay unchanged over a few in-game days; no condition or abandonment problems | 8 | suite-slow |
| ZONING-002 | A deformed zoned growable gets no condemned icon when a neighbouring cell is rezoned; its zone stays under it; residents stay; after a reset it still stands | 103 | suite-slow |
| ZONING-003 | With Plop the Growables, plopped and zoned deformed growables stay; PTG's info is unchanged; no errors | 104 | suite-slow, needs-mod:PlopTheGrowables |
| ZONING-004 | Growables deformed under 1.0.0 show no zoning under them; a reset one with its zone painted again stays | 105 | suite-slow, needs-save:1.0 |

## SAVE – Saving and loading (`specs/save.spec.ts`)

| ID | Requirement | Rows | Check |
|---|---|---|---|
| SAVE-001 | Deformed buildings and their lot areas and paths stay deformed after save + reload and after a restart; logs `Saved cages: N buildings on M variants …, 0 failed` | 10, 19 | suite-slow |
| SAVE-002 | Saving logs `Save: N deformed buildings as base + cage …` and `Save: N buildings back on their variants`; linked, split-with-blend and flat-terrain shapes come back; no `Unknown prefab ID` for `__uq` names | 188 | suite |
| SAVE-003 | Five save + reload cycles don't move paths, areas or lot props | 189 | suite |
| SAVE-004 | Without Unsquare, a save with deformed buildings loads with plain buildings and the deformed lot layout; the game log shows `Not serializable type …SavedCage` and no errors | 191 | suite-slow |
| SAVE-005 | A 1.1 save saved again loads its shapes from the save even with the manifest files moved aside | 192 | suite-slow, needs-save:1.1 |
| SAVE-006 | After raising `<Version>` and rebuilding, a save still restores its shapes | 197 | suite-slow, needs-build |
| SAVE-007 | With the manifest files moved aside, a new-format save loads without crash: `Restored 0 variants`, `Saved cages: …, 0 failed`, no `has no instance archetype` or `Not swapping to …` | 198 | suite-slow |
| SAVE-008 | With `variants.json` deleted, loading a save with deformed buildings: what breaks is documented | 15 | claude:manifest |

## LEGACY – Old manifest (`specs/legacy.spec.ts`)

| ID | Requirement | Rows | Check |
|---|---|---|---|
| LEGACY-001 | Legacy entries unused for over 14 days with no city are pruned at start; their assets stay; the entry is kept in `variants.archive.json` | 14 | suite-slow, needs-save:1.1 |
| LEGACY-002 | At start every manifest entry is appended to the archive (`Legacy manifest archive holds N variants`); the housekeeping line names pruned, evicted, cache and legacy-claimed MB | 199 | suite-slow, needs-save:1.1 |
| LEGACY-003 | New deformations never add manifest entries | 203 | suite-slow |
| LEGACY-004 | A 1.1 save saved over itself and reloaded is gone from its entries' `Cities` | 204 | suite-slow, needs-save:1.1 |
| LEGACY-005 | A missing manifest is rebuilt from the archive (`Variant manifest rebuilt from the legacy archive`); a 1.1 save loads with its shapes | 205 | suite-slow, needs-save:1.1 |
| LEGACY-006 | *I've converted all my old saves* empties the manifest; no legacy restore at the next start; converted saves load fine, unconverted ones show white boxes | 206 | suite-slow, needs-save:1.1 |
| LEGACY-007 | *Restore shapes for old saves* reports the restored count; the old save loads with its shapes | 207 | suite-slow, needs-save:1.1 |
| LEGACY-008 | Retiring with failed legacy restores names the city's failed buildings (at most 5 plus "and N more"), asks to deform them again, logs all; the manifest is empty afterwards | 208 | suite-slow, needs-save:1.1 |
| LEGACY-009 | Loading a 1.1 save opens the old-saves popup with the count (log `N deformed buildings were loaded in the old save format`); *I still have old saves I need to keep* closes it; it opens again on the next load | 209 | suite+review:toolwindow, needs-save:1.1 |
| LEGACY-010 | With *Hide the old-saves reminders* on the popup stays closed; after Reset settings it is back | 210 | suite, needs-save:1.1 |
| LEGACY-011 | The popup's *I've converted all my saves* shows the result (and failed buildings, if any); after resaving and a restart the manifest is empty and the save loads with its shapes | 211 | suite-slow, needs-save:1.1 |
| LEGACY-012 | No popup for a new-format save, a resaved 1.1 save, or a new city | 212 | suite, needs-save:1.1 |
| LEGACY-013 | After a retire, an unconverted 1.1 save shows the restore popup with the count; *Restore shapes* restores only this city's variants and the next load shows the convert popup | 218 | suite-slow, needs-save:1.1 |
| LEGACY-014 | A converted save loaded while the manifest has entries shows the finish popup with the entry count; none in a plain city, a new game, or with the reminders hidden | 219 | suite, needs-save:1.1 |
| LEGACY-015 | Pre-release manifests are discarded with `Variant manifest has an old format …` | Setup 5 | suite-slow, needs-save:prerelease |

## CACHE – Geometry cache (`specs/cache.spec.ts`)

| ID | Requirement | Rows | Check |
|---|---|---|---|
| CACHE-001 | With a 0.5 GB cache, housekeeping at start evicts assets (count and MB logged); the folder holds about 0.5 GB plus legacy-claimed assets | 200 | suite-slow |
| CACHE-002 | Loading a save whose shapes were evicted rebakes them (`Saved cages: … new assets`), also on a second load in one session | 201 | suite-slow |
| CACHE-003 | Legacy-claimed assets are never evicted: a 1.1 save keeps its shapes | 202 | suite-slow, needs-save:1.1 |

## OPTIONS – Settings (`specs/options.spec.ts`)

| ID | Requirement | Rows | Check |
|---|---|---|---|
| OPTIONS-001 | Options → Unsquare shows Settings as the first tab and Key bindings second, with all groups and their defaults | 22 | claude:options |
| OPTIONS-002 | Every settings change logs `Options: …` once | 22 | suite |
| OPTIONS-003 | Line thickness, handle sizes and axis length apply to the overlay at once | 26 | suite |
| OPTIONS-004 | Reset settings restores the defaults, also after a restart, and keeps key bindings | 27 | suite-slow |
| OPTIONS-005 | Pick radius, far-click margin and gizmo size take effect at once | 42 | suite, input |
| OPTIONS-006 | At a 20 px drag threshold small mouse jitter on a press doesn't drag | 87 | suite, input |

## UI – Tool window and button (`specs/ui.spec.ts`)

| ID | Requirement | Rows | Check |
|---|---|---|---|
| UI-001 | Without a target there is no window; selecting a building shows it centred above the roof | 58 | suite+review:toolwindow |
| UI-002 | The window follows the roof when panning, rotating and zooming, stays above the highest corner, stops at the screen border and hides with the building behind the camera | 59 | suite+review:toolwindow |
| UI-003 | The pick buttons switch the pick mode and the highlighted button always matches it | 60 | suite+review:toolwindow |
| UI-004 | The reset-part button is disabled without a handle; with one it resets only that handle, undoable | 61 | suite+review:toolwindow |
| UI-005 | The reset-building button restores the original shape (`[Edit] Reset -> …`), undoable | 62 | suite |
| UI-006 | Clicking a window button keeps selection and target; the click reaches neither building nor ground | 63 | suite, input |
| UI-007 | The window hides with the target and stays hidden outside the mode | 64 | suite+review:toolwindow |
| UI-008 | The gizmo move/rotate toggle sits under the pick toggle and switches like a Ctrl tap | 178 | suite+review:toolwindow |
| UI-009 | Switching the align section to height turns the side buttons into level buttons (log `Levelling …`) and dims the align mode toggle; switching back aligns again | 181 | suite+review:toolwindow |
| UI-010 | The window's undo and redo buttons work and are disabled when there is nothing to undo or redo | 182 | suite+review:toolwindow |
| UI-011 | The button position option moves the Unsquare button at once (top left, top right, bottom toolbar); it is never shown twice, highlights while the mode is on and toggles it everywhere | 183 | suite+review:toolwindow |

## FLEXIT – FlexIt import (`specs/flexit.spec.ts`)

| ID | Requirement | Rows | Check |
|---|---|---|---|
| FLEXIT-001 | Without FlexIt, Import reports `FlexIt is not running` and changes nothing; in the main menu the button is disabled | 98 | suite |
| FLEXIT-002 | Import converts taper, square end and skew-only buildings (props and paths now follow), fits corner buildings, resets arches; no skew is left; log `FlexIt import done: 4 converted (1 approximated, …), 1 curved reset, 0 failed` | 99 | suite+review:foreign, needs-mod:FlexIt |
| FLEXIT-003 | An imported building has an applied cage with the FlexIt shape and edits and resets like any Unsquare building | 100 | suite, needs-mod:FlexIt |
| FLEXIT-004 | After saving and removing FlexIt, imported buildings look the same; no `Unknown prefab ID` for them | 101 | suite-slow+review:foreign, needs-mod:FlexIt |
| FLEXIT-005 | Corner fits keep their far end facing the turned street; 10° looks close to FlexIt, 30° visibly straighter; log `approximated corner fit … (max deviation X m)` | 102 | suite+review:foreign, needs-mod:FlexIt |

## PLACEMAKER – Placemaker compatibility (`specs/placemaker.spec.ts`)

| ID | Requirement | Rows | Check |
|---|---|---|---|
| PLACEMAKER-001 | Hovering a row house Placemaker bent previews its cage in the bent shape; clicking opens the dialog "Building shaped by Placemaker", without cage or tool window; other clicks do nothing | 165 | suite+review:toolwindow, needs-mod:Placemaker |
| PLACEMAKER-002 | Cancel or right-click selects nothing; hotkeys and undo can't edit it; clicking it again asks again | 166 | suite, needs-mod:Placemaker |
| PLACEMAKER-003 | Convert only this building shows the cage in the bent shape at once and the house looks the same after the bake; log `adopting … exact` or `fitted (max deviation X m)` | 167 | suite+review:foreign, needs-mod:Placemaker |
| PLACEMAKER-004 | A converted and edited house restores after reload as an Unsquare variant on the plain model; clicking it opens no dialog | 168 | suite, needs-mod:Placemaker |
| PLACEMAKER-005 | Reset turns a converted house plain; Placemaker may bend it again, and then the dialog opens again | 169, 177 | suite-slow, needs-mod:Placemaker |
| PLACEMAKER-006 | The dialog offers Convert only this building and Cancel, and Convert whole row (N houses) first in a joined row with N the joined houses; never Deform on top | 170, 172 | suite+review:toolwindow, needs-mod:Placemaker |
| PLACEMAKER-007 | Without Placemaker, clicking a building opens no dialog | 171 | suite |
| PLACEMAKER-008 | Convert whole row swaps all N houses in one frame with their shapes; neighbours stay joined; log `Row conversion of building …: N of N converted`; Placemaker's next plan moves none of them | 173 | suite, needs-mod:Placemaker |
| PLACEMAKER-009 | Converting a single house of a row lets Placemaker straighten its neighbours (known behaviour) | 174 | suite, needs-mod:Placemaker |
| PLACEMAKER-010 | Resetting one converted house of a row turns only that one plain | 175 | suite, needs-mod:Placemaker |
| PLACEMAKER-011 | Each load logs `Placemaker <version> (tested with <TestedWith>): detect ok, convert ok, rows ok.` once | 176 | suite, needs-mod:Placemaker |
| PLACEMAKER-012 | Converted houses keep their shapes over save and reload; undecided ones are bent by Placemaker again; no house changes shape after the load | 196 | suite-slow, needs-mod:Placemaker |

## COPY – Copies of deformed buildings (`specs/copy.spec.ts`)

| ID | Requirement | Rows | Check |
|---|---|---|---|
| COPY-001 | Eyedropper (or Find It) copies of a deformed house and office don't crash, have the deformed shape and open their info panel | 161 | claude:placement |
| COPY-002 | Copies placed before the repair are repaired at load (`Repaired N copies of variants missing instance components`) without a crash | 162 | suite, needs-save:copies |
| COPY-003 | A copy of a deformed service building with an upgrade doesn't crash on hover or click | 163 | claude:placement |
| COPY-004 | Copies get residents or companies, aren't condemned, and Reset gives the plain building | 164 | claude:placement |
| COPY-005 | Move It and eyedropper copies keep their shape over save and reload | 195 | claude:placement |

## Review packs (`/ingame-review`)

The suite writes each pack to `.testruns/<run>/review/<ID>/` with `brief.md`, screenshots from fixed camera presets
and the probe and log data. Claude judges only what the suite couldn't measure.

| Pack | Look for |
|---|---|
| `mesh` | Smooth surfaces without kinks, holes, stretched or flipped faces; lighting continuous across the deformed walls; plain buildings at every LOD without missing or pink parts |
| `overlay` | Cage colour matching the state (white undeformed, yellow pending, green applied, red failed, magenta outside the plot); dashed rest box; orange selection; cyan hover; gizmo arrows, square and rings; ghost cage in split modes |
| `toolwindow` | Window placement above the roof; highlighted, dimmed and disabled buttons matching the bindings; button position; dialogs and popups with the expected texts and buttons |
| `ground` | Paths connected and bending smoothly (no kinks, loops or arcs); gardens flush with moved walls; nothing outside the lot |
| `foreign` | Before/after shots of FlexIt and Placemaker buildings showing the same shape, or the expected approximation |

## Charters (`/ingame-explore`)

| Charter | Goal | Steps | Pass |
|---|---|---|---|
| `options` | OPTIONS-001 | Open Options → Unsquare (cohtml inspector if available, else screenshots and clicks); list tabs and groups; compare with `UnsquareSettings` defaults | Tab order and every group with its default value |
| `naming` | UPGRADE-009 to 011 | Deform the school, hospital and car park of the fixture (bridge); open each info panel, the upgrade menu, build menus, Find It and the milestone screen | Normal names, Upgrades sections as listed, no `__uq` or duplicate entry |
| `placement` | TERRAIN-006, UPGRADE-012/013, COPY-001 to 005 | Use the game's road, upgrade, eyedropper and Move It tools through bridge input; check shapes with bridge probes; save and reload where the requirement says | As in each requirement; no crash |
| `manifest` | SAVE-008 | Close the game, move `variants.json` aside, start, load a save with deformed buildings, note what breaks, move the file back | A short written finding in the report |
