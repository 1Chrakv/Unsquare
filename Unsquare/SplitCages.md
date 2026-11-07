# Split cages: building and lot (concept)

Status: implemented on `feature/split-cages` (steps 1–4 of section 8); in-game tests (`Testing.md` 126–138)
pending. Model, UI and persistence decided with the user; reviewed against the code in a separate design review.

## 1. Goal

Today one cage (the lot box) deforms everything: the building mesh, its props, ground props, areas, lanes, nets
and the lot terrain. Players want to deform the **building** (mesh and what hangs on it) and the **lot** (ground,
paths, ground props, terrain) separately, and still both together.

Three edit modes:

| Mode | An edit moves | Typical use |
|---|---|---|
| Building | the building cage | lean or taper a tower without bending its garden or car park |
| Lot | the lot cage | fit paths, lawns and the terrain to an odd plot; the building stays as it is |
| Both | both cages by the same delta | today's behaviour |

## 2. Model: two cages per building

Each building gets two cage offset sets instead of one:

- **Lot offsets**: deform the lot (section 3).
- **Building offsets**: deform the building mesh and attached props.

Both are stored on the **lot box** (`LotBox`), like the single offsets today. That keeps one coordinate frame for
identity, naming and undo.

- Equal offsets ("linked") reproduce today's behaviour exactly. Every existing variant is a linked variant and
  keeps its v1 name and its `DeformKey(lotBox, offsets)` assets.
- The mode is an **edit target**, not a property of the building: it only decides which offsets an edit writes.
- On a fixed box the trilinear deformation is linear in its offsets, so "Both adds the same delta to both" is
  exact superposition: the building's own shape is kept on top of the lot's.
- `DeformSpec` becomes (lot offsets, building offsets, flat terrain).

### 2.1 Editing the building cage on its own box

The UI shows and edits the building cage on the building's own box (7.3). Converting between the two boxes is not
exact on the 0.1 m grid:

- Lot box → building box is an interpolation: the view is finer than the stored grid (error ≤ 0.05 m).
- Building box → lot box extrapolates and amplifies rounding by about lot size / building size per axis. Converting
  absolute offsets back would turn a nudge up and down into a different lot-frame value: the building silently
  becomes split (new name, new bake, ghost cage) although nothing visible changed.

Rules:

- **Building-mode edits convert deltas, never absolute offsets.** The conversion `E` (building box → lot box) is
  linear, so `round(E · d)` is symmetric and +d followed by −d returns exactly. Drags use the lot-frame offsets at
  drag start plus `round(E · totalDelta)`, so they never drift.
- **History records lot-frame specs.** `CageEditSession.RememberEdit` stores what `TryGetCage` returns in the edit
  frame today; it must store the lot-frame spec, so undo and redo replay without converting again. The
  `DeformService` edit-offsets cache only covers the last request and is no substitute.
- **Commands that compute absolute building-box offsets** (`GroundFit.Fit`, `NeighbourAlign.Align` /
  `FitHeight`, `CageOffsets.Reset(handle)`) apply `result − view` as a delta, where view is the current building
  cage on the building box. Otherwise fit, align and reset handle would bypass the delta rule.
- **Snap rules**, so rounding never leaves a building split or deformed by accident:
  - building-box view equals the lot cage converted to the building box → building offsets := lot offsets;
  - building-box view is identity → building offsets := identity (e.g. after resetting every handle one by one).
- **Clamping, validation and twist run on the building box values** (convert first), in all three places:
  `DeformService.ApplyLot` (clamp; on the lot box it would silently change the building),
  `VariantSystem.TryGetOrCreateVariant` (`CageValidator.Validate`; on the lot box it rejects buildings whose
  extrapolation folds outside the building box) and `CageTwist.MaxResidualMeters` (the plan twist grows by
  `(Lx · Lz) / (Bx · Bz)`, so a few centimetres on a small house would drop the coarse LOD).

## 3. What follows which cage

| Part | Source in the prefab | Follows |
|---|---|---|
| Building meshes and LODs | `m_Meshes` | building cage |
| Attached props, attached sub-buildings | `ObjectSubObjects`, `m_ParentMesh >= 0` | building cage |
| Ground props inside the building footprint (steps, AC units) | `ObjectSubObjects`, `m_ParentMesh == -1` | blend (3.1) |
| Ground props elsewhere, trees | `ObjectSubObjects`, `m_ParentMesh == -1` | lot cage |
| Markers (spawn locations, entrances) | `ObjectSubObjects` | blend, rigid pose from its Jacobian |
| Areas (surfaces, hedges) | `ObjectSubAreas` | per node: parent mesh → building cage, ground → blend |
| Lanes (paths, fences, parking) | `ObjectSubLanes` | per end: parent mesh → building cage, ground → blend |
| Nets (paths, small roads) | `ObjectSubNets` | blend, purely positional |
| Terrain lift, flat-terrain flag | `LotLiftSystem` | lot cage |
| Fit to ground | `GroundFit` | the active cage (7.6) |
| On-lot upgrade storeys | extensions | building cage |
| Upgrade extensions | extensions | by position: mesh box over the building footprint → building cage, otherwise lot cage or blend |

Far from the building the blend equals the lot cage, so "blend" only differs from "lot cage" near the building.

Ground extensions (car parks, helipads) are classified by position because the building cage is small: its taper
and twist extrapolated 20–30 m away would shear them badly.

### 3.1 Blend between building and lot

Anything that connects the building to its grounds would tear when the cages differ: lanes into the entrance, path
nets to the door, markers, steps at the wall. They use a **composite deformation** that follows the wall and fades
out:

`D(p) = L(p) + w(p) · Δ(c(p))`, with `Δ = B − L`

where `B` and `L` are the building and lot deformations and `c(p)` is p clamped to the footprint in xz.

- Inside the footprint (mesh box, `BuildingBox`) `D = B`. Just outside, a point takes exactly the change of the wall
  beside it and never more: the building cage is not extrapolated (in-game test: a pulled corner sheared a hospital's
  garden and pushed hedges off the lot when it was).
- `w` depends only on the horizontal position: the normalised distance outside the footprint (per side, relative to
  that side's ring width), faded by smoothstep.
- Ring width per side (`CageBlend.Ring`, `BlendRing`): `min(m, max(gap to the lot edge, fold))` with
  `fold = MarginPerMeter · max |Δ|` at the footprint corners and `m = max(MarginMeters, fold)`. The ring ends at the
  lot edge where folding allows; where the wall comes closer to the lot edge, it is as narrow as it can be without
  folding (`1 − 1.5 · |Δ| / width ≥ 0.25`).
- Linked cages give `Δ = 0`, so `D = L`: exactly the single-cage result.
- Points attached to a mesh (lane ends with `m_ParentMesh >= 0`, area nodes with `m_ParentMeshes[node] >= 0`) follow
  the building cage (attachment, not blending).
- Nets stay purely positional: `NetInstanceSync` remaps instance points without knowing the prefab curves.
- The Jacobian is `J_L(p) + w · J_Δ(c) · ∂c/∂p + Δ(c) ⊗ ∇w`; rigid poses and the Newton inversion in net sync need
  the full form. `D` keeps a fold check in validation as a backup.

**Blend mode per building** (`BlendMode`, part of the variant name and manifest):

| Mode | Blend (D) | Lot cage |
|---|---|---|
| All (default) | paths, lanes, markers, ground props over the footprint, ground area nodes | everything else on the ground |
| Paths | paths, lanes, markers | gardens, ground props, areas |
| None | nothing (paths into the building may break) | everything on the ground |

`SplitDeformations.Paths` and `.Ground` resolve this; net remaps use `Paths`. Linked specs ignore the mode.

**The ring is a format rule, never a setting.** Saved nets are remapped by inverting the *old* deformation, which is
rebuilt from the variant name (`VariantSystem.BuildNetRemap`). `MarginMeters`, `MarginPerMeter` and the ring rule
are fixed like `CageOffsets.StepMeters`, so the ring is determined by the spec, i.e. by the name.

### 3.2 Facade alignment

Props near a facade turn with it today (`FaceAlignment`). With split cages each prop aligns using the deformation it
follows: attached props the building cage, ground props elsewhere the lot cage, blended props `D`.
`AttachmentContext` carries the deformations; `AlignmentBox` is already the mesh box. Revisit if gardens look wrong
in game.

## 4. Variants and baking

- **Linked spec**: name, assets and manifest entry exactly as today.
- **Split spec**: new name seed `Unsquare:split:v1:` + lot offsets + `|` + building offsets (+ `|flat-terrain`).
- **Building cage identity, lot deformed** ("lot only"): meshes are not rebaked (`TryDeformMeshes` skips the bake);
  the clone reuses the base meshes, so the variant has no geometry assets. Only attachments and terrain change. A
  lot-only owner can leave mesh-only storey extensions on their base prefab, which avoids the problem in
  `Upgrades.md` 5.8 for those buildings.
- **Lot cage identity, building deformed**: meshes and attached props baked as today. Ground attachments far from
  the building stay unchanged, but blended ones near it (3.1) do change. The terrain stays the game's flat pad.
- Mesh bake keys (`DeformKey`) depend only on the building offsets, so a building shape is baked once and shared
  by variants that differ only in their lot.
- The coarse-LOD twist check (`CageTwist`) is measured on the building box (2.1).
- `VariantSystem.Request` collapses identity specs to `DeformSpec.Identity` only when **both** cages are identity,
  and normalises `FlatTerrain` to false when the lot cage is identity; otherwise one shape gets two names.

## 5. Persistence and compatibility

Confirmed by the user: no compatibility is needed for players going back to an older mod version.

- `ManifestEntry` gets an optional `BuildingOffsets`; missing or null means "equal to `Offsets`" (linked).
- `VariantManifest.FormatVersion` goes to 3. `VariantManifest.Load` currently **drops** entries of other versions;
  it must migrate version 2 entries (linked variants, nothing to convert) instead.
- `VariantManifest.Register` copies `Offsets` and `FlatTerrain` when it updates an entry; it must copy
  `BuildingOffsets` too.
- **Orphan sweep:** `PrunePlanner.PlanOrphanSweep` refuses the whole sweep when any entry lists no assets. Lot-only
  variants have none by design, so the sweep would never run again. The refusal must only apply to entries that
  should have geometry (building cage not identity).
- Preload restore builds split variants from both offset sets; extensions seed on their owner's variant name
  (`ExtensionVariantName`, unchanged).

## 6. Pipeline and service changes (outline)

- `DeformSpec`: add `BuildingOffsets`; `IsLinked`; `WithLotOffsets` / `WithBuildingOffsets`.
- `VariantNaming.VariantName(baseName, spec)`: v1 seed when linked, split seed otherwise.
- `VariantPrefabFactory.TryCreate`: two deformations plus the composite; meshes and attached props get `B`, the
  rest `L` or `D` per section 3. `AttachmentContext` carries all three.
- Everything that reads `Offsets.IsIdentity` today must ask the right cage: `CageState.IsDeformed` /
  `TerrainLift`, `DeformService.SetTerrainLift`, `MapStatus`, `ExtensionInstallSystem` and
  `ProcessExtensionRefreshes`.
- `BuildNetRemap`, `BuildExtensionSwaps` and `TryGetVariantCage` (`LotLiftSystem`) use the composite or the lot cage.
- `IBuildingDeformService`: `SetOffsets` / `MoveHandle` / `Reset` take a target (`CageTarget`: Building, Lot,
  Both); `CageState` exposes both offset sets, both target specs and the building box.
- `DeformService` edit boxes per mode (same as 7.3): Building mode uses the building box raised to deformed on-lot
  storeys; Both mode keeps today's raised edit box (`BuildingDescription.EditBox`), so a storey stays inside the
  cage and Both behaves exactly like today; Lot mode uses the plain lot box (lot items sit on the ground, the top
  barely matters). Building-mode edits follow 2.1 (deltas, snap to linked, checks on the
  building box). Neighbour edges come from other buildings' building cages in Building mode and from their lot
  cages otherwise (`NeighbourQuery` already finds the buildings; only the edge source changes).
- **Vertical seating:** Lot-mode edits never move the building, also not vertically. The building can end up buried
  or floating (Lot-mode fit or align ground, moving a lot column, Lot reset, Building reset). The service reports
  it in `CageState` and the tool window shows a hint (7.8); Fit to ground in Building mode reseats the building.
- **Seating is measured on the ground plane**, not at the mesh box's bottom corners (basements reach below ground,
  some meshes start above it). At the building box's ground points (`GroundFit.GroundPoints3`, where y = 0 cuts
  each column) compare the displacements `B(p).y − L(p).y`; off by more than a tolerance → hint. This is exactly 0
  for linked cages by construction. Building-mode fit makes it 0, applied as a delta (2.1).
- `CageEditSession`: `EditMode` (Building, Lot, Both) with `SetEditMode` / `CycleEditMode`, `LinkBuildingToLot`;
  every command reads the mode. The mode is UI state, not part of the spec, so mode switches are not undoable
  (like selection changes); Link is an edit and is undoable. History stores lot-frame specs (2.1).
- `UnsquareUISystem` bindings and `UnsquareSettings` (hidden value for the remembered mode, two key bindings).
- Core gets tests for: delta conversion (+d/−d round trip, no drift, absolute commands as deltas), both snap
  rules, seating (0 for linked cages), the composite deformation
  (continuity, linked = today, Jacobian incl. `∇w`, fold check, margin scaling), naming (linked names unchanged),
  identity normalisation, manifest migration and `Register`, and the orphan sweep with asset-free entries.

## 7. UI

Decided step by step with the user.

### 7.1 Mode names and switch

- Modes: **Building**, **Lot**, **Both**.
- New first section **Deform** at the top of the tool window, above Pick: a 3-way `SegmentedToggle` with icons
  and localised tooltips, plus a **Link** button (7.5).
- Hotkeys: Alt+D cycles the mode, Alt+K links the building to the lot (both free today; Alt+L is "log state").

```
┌─ Deform ─────────────────────────────┐
│ [▣ Building][▭ Lot][▣▭ Both]  [Link] │
├─ Pick ───────────────────────────────┤
│ [corner][edge][face]                 │
├─ Terrain ────────────────────────────┤
│ ...                                  │
```

### 7.2 Default mode

- **Both** on first start, so nothing changes for existing players.
- The last chosen mode is kept for the next picked building and saved in the settings (hidden value), so it
  survives a restart.

### 7.3 Building cage shape

- **Building mode** shows and edits the building cage on a **tight box around the building**: the mesh box
  (`BuildingBox`), raised to deformed on-lot storeys like the edit box today. Handles sit on the building's own
  corners. Flat or meshless lots (parks) fall back to the lot box.
- **Both mode** shows today's edit box: the lot box, raised to deformed on-lot storeys, so it behaves exactly like
  today.
- **Lot mode** shows the plain lot box (lot items sit on the ground; storeys belong to the building).
- Stored on the lot box either way (section 2, conversion rules in 2.1).

### 7.4 Showing the other cage

- The active cage is drawn as today (solid, handles, gizmo).
- The other cage is drawn **faint and dashed, without handles**, but only when the building is split. Linked
  buildings look exactly like today.
- New `CageOverlayStyle` entries for the ghost cage (colour, alpha); `SegmentDashes` already exists for dashing.

### 7.5 "Both" when the cages differ

- An edit in Both mode moves the lot cage and adds the **same change** to the building cage, so the building follows
  directly, live during the drag, and keeps its own shape on top (a tower leaning 2 m still leans 2 m after the lot
  is widened). No Link click is needed for that.
- Both adds deltas rather than chaining the deformations: rotating the lot (e.g. by align) moves the building with
  it but does not rotate the building's own lean.
- **Link** is only for discarding the building's own shape: it snaps the building cage onto the lot cage (building
  offsets := lot offsets); greyed while linked. A second Link (Alt+Shift+K) does the reverse: the lot takes the
  building cage's shape (lot offsets := building offsets).
- **Blend mode** (3-way toggle next to Link, Alt+B; greyed while linked): All, Paths or None (3.1); undoable.

### 7.6 Terrain, fit to ground and align per mode

| Tool | Building | Lot | Both |
|---|---|---|---|
| Lift / flat toggle | greyed, tooltip "terrain belongs to the lot" | as today | as today |
| Fit to ground | seats the building on the lot's ground (at its ground points, 6) | as today, building stays | as today, building carried along |
| Ground fit mode (back only / all) | greyed (seating uses all corners) | as today | as today |
| Align to neighbour | moves the building cage's face | as today | as today, building carried along |
| Align ground to neighbour | moves the building's columns | as today, building stays | as today, building carried along |

Building-mode align lets row houses be made flush with their neighbours while their gardens stay.

Align targets per mode:

- **Building mode** aligns to the neighbours' **building cages** (their tight building box with their current
  building deformation), so walls meet walls. Neighbours without one (parks, meshless or flat lots) fall back to
  their lot edges; roads stay candidates. Reach is today's; the building face may move beyond its own lot border.
- **Lot and Both mode** align to the neighbours' lot cages and roads, as today.
- Align ground to neighbour uses the same target: in Building mode the height of the neighbour building's bottom
  edge, otherwise the neighbour lot's ground as today.
- Same rules as today for facing (outward normal must face the face), reach and the angle / flush / full modes.
- The mesh box is the bounding box of all meshes (porches, steps, eaves), so "walls meet walls" is approximate.

### 7.7 Reset

Reset acts on the active cage:

- **Building**: the building cage goes back to the original building shape (identity); the lot is untouched.
- **Lot**: the lot goes back to the original; the building keeps its shape (now split if it was linked).
- **Both**: everything back to the original prefab, as today.
- Reset handle follows the same rule for the selected handle.
- Link (7.5), not reset, is the way to make the building follow the lot again.
- "Reset all buildings" (settings, Alt+Shift+Delete) stays a full reset regardless of mode.

### 7.8 Seating hint

- Lot mode never moves the building (6, vertical seating). When the building is off the lot's ground (measured at
  its ground points, 6), the tool window shows a hint, e.g. "Building is not on its ground: use Fit to ground in Building mode".
- Shown in every mode while the condition holds; no automatic correction.

### 7.9 Texts

- Tooltips and option texts in `LocaleEN` and the UI module; README gets a short section on the modes. The
  long description in `PublishConfiguration.xml` gets one plain sentence (building and grounds can be shaped
  separately), no hotkeys.

## 8. Implementation order

Each step is its own commit on `feature/split-cages`; the game must behave exactly as today until step 4.

1. Core: `DeformSpec` with two offset sets, naming (linked = v1 names), identity normalisation, manifest field,
   v2 → v3 migration and `Register`, orphan sweep, delta conversion and snap to linked, composite deformation with
   fold check; tests first.
2. Pipeline: two deformations and the composite in `VariantPrefabFactory`, checks on the building box, mesh-free
   lot-only variants, `LotInstanceSync` and net remap with the composite, extensions by position, preload restore.
3. Service: edit targets, building box conversion, seating and seating report, building-cage neighbour edges.
4. UI: session mode and Link, lot-frame history, Deform section, ghost cage, greyed tools, seating hint, hotkeys,
   texts.
5. In-game tests (`Testing.md`): linked buildings unchanged after load, seams on several building types, folding
   with a building grown over its garden, storeys and ground extensions, FlexIt import still producing linked
   variants.

## 9. Risks

- **Seams and folding** (3.1) are the main risk: lanes into doors, markers and paths near a grown building must be
  checked in game on several building types.
- **Vertical seating:** Lot-mode edits can bury or lift the building; the hint and Building-mode fit cover it, but
  players may not notice it right away.
- **Ground extensions** are classified by position; extensions straddling the footprint edge need a look in game.
- Ground props outside the footprint can end up inside walls when the building grows over them. This is accepted
  (it is what "building only" means), but worth a hint in the README.
- More variants per base prefab (lot and building shapes multiply); mesh sharing by bake key limits the assets.
- Upgrades and extensions: the combined cage design in `Upgrades.md` must be re-read before touching extensions.
