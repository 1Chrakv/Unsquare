# Split cages: building and lot (concept)

Status: concept, no code yet. Model and UI decided with the user (sections 2 and 7); persistence (section 5)
still needs confirmation before implementation.

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
identity, naming and undo. The UI shows and edits the building cage on the building's own box (7.3) and converts
with `ExtendedCage.Convert`, the same way the raised edit box works today:

- Building box → lot box extrapolates (a small house on a big lot amplifies by lot size / building size), so the
  stored 0.1 m grid is coarser than 0.1 m on the building box after converting back. `DeformService` already keeps
  the last edit offsets per building so a nudge is never swallowed; that mechanism is reused per cage.
- `CageLimits` / `CageValidator` must check the building cage on the box it is edited on, not on the extrapolated
  lot-box values (they can exceed the limits without the building being deformed much).
- If extrapolation turns out too coarse in game, the fallback is storing the building offsets on the building box
  with an explicit "linked" state (null building offsets) instead of equality; naming would follow that.

- Equal offsets ("linked") reproduce today's behaviour exactly. Every existing variant is a linked variant.
- The mode is an **edit target**, not a property of the building: it only decides which offsets an edit writes.
  "Both" adds the same delta to both, so a difference between the cages is kept.
- `DeformSpec` becomes (lot offsets, building offsets, flat terrain). Undo/redo stores whole specs, so it covers
  both cages without changes to `EditHistory`.

## 3. What follows which cage

| Part | Source in the prefab | Follows |
|---|---|---|
| Building meshes and LODs | `m_Meshes` | building cage |
| Attached props, attached sub-buildings | `ObjectSubObjects`, `m_ParentMesh >= 0` | building cage |
| Ground props, trees | `ObjectSubObjects`, `m_ParentMesh == -1` | lot cage |
| Areas (surfaces, hedges) | `ObjectSubAreas` | lot cage |
| Lanes (paths, fences, parking) | `ObjectSubLanes` | lot cage, blended near the building (3.1) |
| Nets (paths, small roads) | `ObjectSubNets` | lot cage, blended near the building (3.1) |
| Terrain lift, flat-terrain flag | `LotLiftSystem` | lot cage |
| Fit to ground | `GroundFit` | the active cage (7.6) |
| On-lot upgrade storeys | extensions | building cage |
| Upgrade extensions on the ground (car parks) | extensions | split like their owner (meshes: building, ground parts: lot) |

### 3.1 Seams between building and lot

Anything that connects the building to its grounds would tear when the cages differ: lanes into the entrance, path
nets to the door, parking lanes inside a garage, spawn and entrance markers.

Proposal: a **composite deformation** for curves and points near the building,
`D(x) = w(x) · B(x) + (1 − w(x)) · L(x)`, where `B` and `L` are the building and lot deformations and `w` is 1
inside the building's mesh box (`BuildingBox`) and falls smoothly to 0 within a margin (option, e.g. 4 m). It is
continuous, equals `L` away from the building and `B` inside it, and equals today's deformation when the cages are
linked. Lanes and nets use `D` point by point; rigid ground props do not (they are classified strictly, as wanted).

Open questions:

- Markers (spawn locations, entrances) are sub-objects; some have `m_ParentMesh == -1` but belong to a door. They
  should use `D` too (rigid pose from `D`'s Jacobian) rather than the strict prop rule.
- Areas overlapping the building footprint (e.g. a building's own ground surface) may need `D` as well; decide
  after looking at real prefabs in game.
- Parking garages: their car lanes are inside the box and simply follow `B`.

### 3.2 Facade alignment

Ground props near a facade turn with it today (`FaceAlignment`). With split cages the facade belongs to the building
cage while the prop follows the lot cage. Proposal: ground props align to the **lot** cage's faces only (props stay
consistent with their ground), attached props to the building cage. Revisit if gardens look wrong in game.

## 4. Variants and baking

- **Linked spec**: name, assets and manifest entry exactly as today.
- **Split spec**: new name seed `Unsquare:split:v1:` + lot offsets + `|` + building offsets (+ `|flat-terrain`).
- **Building offsets = identity, lot offsets deformed** ("lot only"): meshes are not rebaked; the clone reuses the
  base meshes, so the variant has no geometry assets at all. Only attachments and terrain change.
- **Lot offsets = identity, building deformed**: meshes and attached props baked as today; ground attachments copied
  unchanged; terrain stays the game's flat pad (lift is a lot property, nothing to lift).
- Mesh bake keys (`DeformKey`) depend only on the building offsets, so a building shape is baked once and shared
  by variants that differ only in their lot.
- Coarse-LOD twist check (`CageTwist`) uses the building offsets.

## 5. Persistence and compatibility

Saves depend on prefab names and the manifest, so this needs the user's confirmation before implementation.

- `ManifestEntry` gets an optional `BuildingOffsets`; missing or null means "equal to `Offsets`" (linked). Old
  entries therefore stay valid without rewriting.
- Bump `VariantManifest.FormatVersion` to 3. `VariantManifest.Load` currently **drops** entries of other versions;
  it must migrate version 2 entries (they are linked variants, nothing to convert) instead.
- `IVariantPipeline` restore at preload builds split variants from both offset sets; extensions use the
  building offsets of their owner (`ExtensionVariantName` seeds on the owner variant name, unchanged).
- Prune and orphan sweep need no change beyond reading the new field.

## 6. Pipeline and service changes (outline)

- `DeformSpec`: add `BuildingOffsets`; `IsLinked`; `WithLotOffsets` / `WithBuildingOffsets`.
- `VariantNaming.VariantName(baseName, spec)`: v1 seed when linked, split seed otherwise.
- `VariantPrefabFactory.TryCreate`: two deformations; meshes and attached props get `B`, other attachments get
  `L` or `D`. `AttachmentContext` carries both plus the blend.
- `IBuildingDeformService`: `SetOffsets` / `MoveHandle` / `Reset` take a target (`CageTarget`: Building, Lot,
  Both); `CageState` exposes both offset sets and both target specs.
- `DeformService`: the raised edit box conversion (`ExtendedCage`) applies to the building cage (storeys sit on
  the building); the lot cage always uses the lot box.
- `LotInstanceSync` (areas, nets): remap with the old and new lot (or composite) deformation instead of the single
  cage.
- `GroundFit`, `NeighbourAlign`, terrain lift: operate on the cage(s) chosen in 7.6. `DeformService` builds
  neighbour edges from the other buildings' building cages in Building mode and from their lot cages otherwise
  (`NeighbourQuery` already finds the buildings; only the edge source changes). Seating the building on the
  lot needs the lot's deformed ground height under each building corner (lot cage bottom face, or the lifted
  terrain), computed in Core from the lot cage.
- `CageEditSession`: `EditMode` (Building, Lot, Both) with `SetEditMode` / `CycleEditMode`, `LinkBuildingToLot`;
  every command reads the mode. The mode is UI state, not part of the spec, so mode switches are not undoable
  (like selection changes); `Link` is an edit and is undoable.
- `UnsquareUISystem` bindings and `UnsquareSettings` (hidden value for the remembered mode, two key bindings).
- Core gets tests for the composite deformation (continuity, linked = today), naming (linked names unchanged),
  and manifest migration.

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
- **Lot and Both mode** show the lot box as today.
- Stored on the lot box either way (section 2).

### 7.4 Showing the other cage

- The active cage is drawn as today (solid, handles, gizmo).
- The other cage is drawn **faint and dashed, without handles**, but only when the building is split. Linked
  buildings look exactly like today.
- New `CageOverlayStyle` entries for the ghost cage (colour, alpha); `SegmentDashes` already exists for dashing.

### 7.5 "Both" when the cages differ

- An edit in Both mode moves the lot cage and adds the **same change** to the building cage, so the building follows
  directly, live during the drag, and keeps its own shape on top (a tower leaning 2 m still leans 2 m after the lot
  is widened). No Link click is needed for that.
- **Link** is only for discarding the building's own shape: it snaps the building cage onto the lot cage (building
  offsets := lot offsets); greyed while linked.

### 7.6 Terrain, fit to ground and align per mode

| Tool | Building | Lot | Both |
|---|---|---|---|
| Lift / flat toggle | greyed, tooltip "terrain belongs to the lot" | as today | as today |
| Fit to ground | seats the building's bottom corners on the lot's ground | as today | as today, building carried along |
| Ground fit mode (back only / all) | greyed (seating uses all corners) | as today | as today |
| Align to neighbour | moves the building cage's face | as today | as today, building carried along |
| Align ground to neighbour | moves the building's columns | as today | as today, building carried along |

Building-mode align lets row houses be made flush with their neighbours while their gardens stay.

Align targets per mode:

- **Building mode** aligns to the neighbours' **building cages** (their tight building box with their current
  building deformation), so walls meet walls. Neighbours without one (parks, meshless or flat lots) fall back to
  their lot edges; roads stay candidates as today.
- **Lot and Both mode** align to the neighbours' lot cages and roads, as today.
- Align ground to neighbour uses the same target: in Building mode the height of the neighbour building's bottom
  edge, otherwise the neighbour lot's ground as today.
- Same rules as today for facing (outward normal must face the face), reach and the angle / flush / full modes.

### 7.7 Reset

Reset acts on the active cage:

- **Building**: the building cage goes back to the original building shape (identity); the lot is untouched.
- **Lot**: the lot goes back to the original; the building keeps its shape (now split if it was linked).
- **Both**: everything back to the original prefab, as today.
- Reset handle follows the same rule for the selected handle.
- Link (7.5), not reset, is the way to make the building follow the lot again.
- "Reset all buildings" (settings, Alt+Shift+Delete) stays a full reset regardless of mode.

### 7.8 Texts

- Tooltips and option texts in `LocaleEN` and the UI module; README gets a short section on the modes. The
  long description in `PublishConfiguration.xml` gets one plain sentence (building and grounds can be shaped
  separately), no hotkeys.

## 8. Implementation order

Each step is its own commit on `feature/split-cages`; the game must behave exactly as today until step 4.

1. Core: `DeformSpec` with two offset sets, naming (linked = v1 names), manifest field and v2 → v3 migration,
   composite deformation; tests first.
2. Pipeline: two deformations in `VariantPrefabFactory`, mesh-free lot-only variants, `LotInstanceSync` with the
   lot / composite deformation, preload restore.
3. Service: edit targets, building box conversion, seating and building-face align.
4. UI: session mode and Link, Deform section, ghost cage, greyed tools, hotkeys, texts.
5. In-game tests (`Testing.md`): linked buildings unchanged after load, seams on several building types,
   storeys and extensions, FlexIt import still producing linked variants.

## 9. Risks

- Seams (3.1) are the main risk; lanes into doors and markers must be checked in game on several building types.
- Ground props can end up inside walls when the building grows over them. This is accepted (it is what "building
  only" means), but worth a hint in the README.
- More variants per base prefab (lot and building shapes multiply); mesh sharing by bake key limits the assets.
- Upgrades and extensions: the combined cage design in `Upgrades.md` must be re-read before touching extensions.
