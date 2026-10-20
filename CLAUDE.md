# Unsquare – Claude guidelines

Cities: Skylines II mod that lets players freely deform buildings by moving the corners, edges and faces of
their bounding cuboid (trilinear cage deformation). Deformed geometry is baked into variant prefabs; see
`Unsquare/HowTo.md` for the pipeline.

## Layout

| Path | Contents | Depends on |
|---|---|---|
| `Unsquare/Core/` | Pure logic: cage model, deformation math, naming, debouncing, persistence, prune planning | `Unity.Mathematics`, `Newtonsoft.Json` only |
| `Unsquare/Integration/` | CS2 integration: prefab cloning, mesh baking, assets, ECS systems | Core + game assemblies |
| `Unsquare/Api/` | UI-facing service (`IBuildingDeformService`) and its data types | Core + Integration |
| `Unsquare/UI/` | The mod's UI: `CageEditSession` (edit model), Unsquare tool (mouse), hotkeys, cage overlay, UI bindings, options page | Core + Api + game input/render/tool systems |
| `UiModule/` | React UI module (game template): mode button next to the underground toggle and the tool window above the edited building (pick mode, resets); talks to `UnsquareUISystem` via bindings | `cs2/*` UI API |
| `Unsquare.Tests/` | NUnit tests; compiles `Core/**` by source link, no game DLLs at runtime | Core |

UI code gets the service via `UnsquareApi.GetDeformService(World)`; cage data is in building space (`BuildingSpace`).
Inputs (`UnsquareToolSystem` mouse tool, hotkeys, later panels) only call `CageEditSession` commands; views only
read the session. Nothing runs outside Unsquare mode (tool active): the session is disabled and hotkeys are ignored.
Selection is two-step (building, then a handle of the pick mode, `HandleSelection`); a selected handle locks the
building until right-click (`CageEditSession.StepBack`).
Picking and gizmo math (`CagePicker`, `AxisDrag`, `PlaneDrag`, `GizmoAxes`) live in `Core/Editing`, in building space.
Drags are axis-constrained (gizmo arrows) or horizontal (gizmo square `GizmoAxes.PlaneXZ`, or pressing on a handle,
which selects it and drags once past `DragThreshold`); horizontal drags keep heights.
Undo/redo (`EditHistory`, Core) is one global history of cage edits; the session records nudges, whole drags and
resets, and undo makes the edited building the target. Selection changes are not undoable.

Dependencies only point downwards (UI → Api → Integration → Core); `UI/` never references `Integration/`.
Never reference `Game`, `Colossal.*`, `UnityEngine` or `Unity.Entities` from `Core/`; the test project would stop
building. In-game testing: `Unsquare/Testing.md`.

## Commands

```bash
dotnet build Unsquare.sln
dotnet test Unsquare.Tests
```

The mod project imports `Mod.props`/`Mod.targets` from `%CSII_TOOLPATH%` (net48, C# 9). Game DLLs are resolved
from `%CSII_MANAGEDPATH%`. Builds deploy to `Mods\Unsquare` and refuse to while the game runs (locked DLL).
The deploy wipes `Mods\Unsquare`; then the build runs `npm run build` in `UiModule/` (needs Node.js). Never pass
`-p:SkipUiModule=true` for a deploy you want to test: without the UI module the Unsquare button is missing.

## Versioning

Semantic version in `<Version>` of `Unsquare/Unsquare.csproj`; bump `ModVersion` in
`Unsquare/Properties/PublishConfiguration.xml` together (the build fails on a mismatch). `BuildInfo` logs version,
commit, configuration and build time on load. Also keep `"version"` in `UiModule/mod.json` equal.
Feature branches only bump the patch version; the minor version goes up once, when the branch is merged into `master`
(the user confirms the number).

## C# conventions (C# 9, .NET Framework 4.8)

- File-scoped namespaces are not available (C# 10); use block namespaces.
- `records` and `init` need `IsExternalInit`; prefer `readonly struct` / sealed classes with constructors.
- Private fields `_camelCase`, everything else PascalCase; constants PascalCase.
- Prefer immutable value types for data passed between layers; validate at the boundary, trust inside.
- `var` when the type is obvious; target-typed `new()`, switch expressions and pattern matching are welcome.
- Make classes `sealed` and members `private`/`internal` unless they are part of the Api.
- No static mutable state except the mod logger; pass collaborators via constructors.
- Throw `ArgumentException` for programmer errors; return `bool Try…(out …)` for expected failures.
- Small, single-purpose types; one public type per file, file name = type name.

## Unity / CS2 / ECS rules

- Every `SystemBase`/`GameSystemBase` subclass must be `partial` (Entities source generator).
- Systems that only work on demand set `Enabled = false` when idle.
- `Unity.Mathematics` (`float3`, `quaternion`, `math.*`) in Core; convert to `Vector3` only at the Mesh boundary.
- `UnityEngine.Object` has overloaded `==`: never use `?.`, `??` or `is null` on Unity objects.
- Never edit shared meshes; `Object.Instantiate` first and `Object.Destroy` every temporary mesh.
- Game meshes pack normals (SNorm16×2) and tangents (Float32×1, sign bit = handedness) octahedrally. Never use
  `mesh.normals`/`tangents` on them; go through `PackedVertexChannel` + `OctahedralPacking` (see `HowTo.md`).
- Buildings have no impostors (CS2 only gives them to trees). The coarsest LOD is left out for twisted cages
  (`CageTwist` > `PipelineOptions.CoarseLodTwistMeters`); it can't follow the bend.
- Mesh and prefab APIs are main-thread only; keep hot loops allocation-free (no LINQ, no closures).
- Wrap each game-API step (clone, AddPrefab, asset I/O, reflection) in try/catch, log, and degrade by not swapping.
- Use `Mod.Log` (`Colossal.Logging.ILog`) for logging, never `Debug.Log`.
- `OverlayRenderSystem` lines/curves are ground-plane only (length measured in xz; vertical ones vanish). Draw 3D
  lines as `CustomMeshType.Cylinder` via `DrawCustomMesh` (see `SegmentPose`).
- Custom meshes are only rendered in frames whose overlay buffer also holds a regular primitive (circle, line);
  `CageOverlaySystem` draws a near-invisible keep-alive circle every frame.
- Prefab names, deform keys, asset names, GUID seeds and the manifest format are what saves depend on. While in
  early development, breaking them is allowed (bump `VariantManifest.FormatVersion`), but only after the user
  confirmed it.

## Options (advanced settings tab)

- Tunables are option objects with a `Default`, not consts: `PipelineOptions` (Core: `CageLimits`,
  `AttachmentOptions`, rebake delay, pruning, sync tolerances, coarse LOD twist limit), `CageEditOptions` and `CageOverlayStyle` (UI).
- Flow: `UnsquareSettings` (Advanced tab) → `SettingsApplier` → `IBuildingDeformService.Configure` →
  `IVariantPipeline.Options` → Core method parameters; edit and overlay options go to the session and overlay.
- Slider attributes are the single source of ranges (mapping clamps to them); defaults come from the option types.
- Attachment options are read when a variant is created; variant names don't include them, so existing
  variants keep their placement until the next load. Never expose values baked into names or assets.

## Lot deformation

- The cage is always the lot box: lot rectangle (`lotSize × 8 m`) from the ground to the roof
  (`CageBoxMeasurer.TryMeasureLot`), at least 8 m tall (`LotBox.MinHeight`) so flat lots have pickable vertical
  edges. Prefabs without a lot are not deformable. A building-only cage was removed
  (0.6.0): it left garden props, ground and paths misplaced.
- The building's mesh box (`TryMeasureMeshes`) is only used as the facade-alignment box for props. Flat or
  meshless lots (parks) fall back to the lot box. The Unsquare tool raycasts building lots, so parks are pickable.
- All attachments (`IAttachmentDeformer`, per kind switchable in `AttachmentOptions`) are deformed in the clone's
  prefab data: props (`ObjectSubObjects`), areas (`ObjectSubAreas`), lanes (`ObjectSubLanes`), nets (`ObjectSubNets`).
- How the game keeps them, and what that means:
  - **Props and lanes** are rebuilt from the prefab when the building is `Updated`, so the swap is enough.
  - **Areas and nets** are saved entities created once by the placement tool. `LotInstanceSync` (called from
    `PrefabSwapper`) rewrites the existing instances from the old to the new prefab layout. Areas are matched by
    ground distance. Nets are remapped point by point (`LotRemap`: invert the old cage, apply the new one), since
    the game splits prefab curves at junctions. Path ends side-connected to a road (the road edge's
    `ConnectedNode` buffer) slide along it (`LotRemap.TryRemapAlong`, distance to the road kept) and get their
    curve position updated; nodes that are the start or end of an outside edge never move.
- Ground items keep their heights (the game projects them on the terrain). Lot size, zone cells and the terrain
  flattening outline stay rectangular; a lot outline leaving its plot only warns (`LotBounds`). Deforming the
  terraform footprint was researched and tried without success: see `Unsquare/LotFootprint.md` before retrying.
  `LotHeightSystem` takes `Lot` edge heights only from neighbouring lots and roads (never the terrain).
- **Terrain lift:** the game flattens the footprint (`BuildingTerraformData` flat area) to one height; `Lot` edge
  heights only shape the strip to the lot edge (`BuildingUtils.SampleHeight`). So for each lifted variant prefab,
  `LotLiftSystem` (after `LotHeightSystem`, Modification5) does two things:
  - It collapses the flat area to the lot centre at the centre lift (`TerrainSlopePlanner`, `TerrainSlopeApplier`).
  - It adds the edge lift, relative to that, to every instance's `Lot`. The game interpolates in between; diagonal
    creases come from its blend.
  - Runtime only: `Lot` is not saved and variant prefabs are rebuilt on load. `LotHeightSystem` rewrites lots near
    changed objects/nets: compare with the written value, never add twice.
  - Stepped `AdditionalBuildingTerraformElement` cells had no visible effect in game; don't retry them.
  - Per building: `DeformSpec` (offsets + `FlatTerrain`) is the variant identity. Flat variants hash the v1 seed plus
    `|flat-terrain` (lifted names are unchanged) and the manifest stores `FlatTerrain`. The toggle is
    `CageEditSession.ToggleTerrainLift` (Alt+T, tool window) and is undoable like any cage edit.
- **Fit to ground** (`CageEditSession.FitToGround`): `GroundFit` moves each corner column so its ground point sits
  on its target and keeps the column height. By default (Alt+G, tool window) the back columns follow the original
  terrain and the street side goes to road level (`GroundFitMode.BackOnly`); Alt+Shift+G fits all four. Only the GPU `TerrainSystem.heightmap` has
  the terrain before lots/roads (CPU heights are read from the modified cascade), so `BaseTerrainSystem` reads a small
  region back asynchronously and samples it with the cascade's `TerrainHeightData` mapping.
- Effects (`EffectSource`) are not deformed yet.
- **Align to neighbour** (`NeighbourAlign`, Core/Editing): the left (+X), back (−Z) or right (−X) face, seen from the
  street, goes to the nearest roughly parallel road or lot edge, outside it or inside up to half the lot depth (a lot
  clipping into its neighbour); lot edges carry an outward normal and must face the face. `AlignMode`: angle keeps the nearer corner,
  flush slides both corners along their edges onto the line, full takes a lot edge's end points (roads: like flush).
  `NeighbourQuery` (Integration) reads road `EdgeGeometry` and buildings from the game's search trees;
  `DeformService` turns other lots into edges via their cages. Alt+Shift+arrows align, Alt+A cycles the mode.

## Props: rigid placement, full deformation later

- **Now:** sub-objects (props, attached sub-buildings) get rigid poses via `SubObjectDeformer` →
  `RigidDeformer.Place`: anchor follows the cage, rotation = polar part of the Jacobian, ground props
  (`m_ParentMesh == -1`) yaw only (`PlacementMode`); attached props use `AttachmentOptions.AttachedPropMode`.
- **Facade alignment:** within `AttachmentOptions.FacadeReachMeters` (default 6 m, 0 = off) of a building box
  face, a prop's rotation blends into that face's rotation, evaluated on the face. Props outside the box align
  fully; ground props only consider walls, attached props also the roof.
- **Why props are not mesh-deformed yet:**
  - Prop meshes belong to shared prefabs, so bending them needs a baked prop variant per prop, placement and
    deformation (incl. LODs), and assets multiply.
  - Saves store every prop instance with its `PrefabRef`; prop variants would need deterministic names and
    preload restore like building variants.
  - Placeholder/random, nested, animated and marker props need special cases. Small props look right when rigid.
- **What full prop deformation would take:**
  1. Per sub-object, clone its `ObjectPrefab` into a prop variant (deterministic name: prop name + parent variant
     hash + sub-object index). Bake meshes with `RenderVariantBuilder`, using sub-object pose ∘ prop mesh pose as
     `SubmeshTransform` (`DeformKey.ForSubmesh` already keys by placement).
  2. Point the clone's `ObjectSubObjectInfo.m_Object` at the prop variant and keep the original pose.
  3. Register prop variants before the parent's `AddPrefab`, list them and their assets in the parent's manifest
     entry, and restore them at preload before the parent.
  4. Resolve placeholder/random props per candidate (or deterministically), recurse into nested sub-objects, and
     hide prop variants from menus/asset browsers.
  5. Bound the cost: fully deform only props over which the deformation is visibly non-rigid (non-linearity
     metric over the prop bounds); keep rigid placement for the rest.

## Comments, tests, commits

- Comments: max 2 lines inside code; XML doc summaries max 3 lines. Explain *why*, not *what*.
- Add NUnit tests for all Core logic; name them `Method_Condition_Expectation`.
- Commit messages: one header line, one blank line, 1–3 summary lines. Keep commits well scoped.
- `README.md` and the `LongDescription` in `Unsquare/Properties/PublishConfiguration.xml` describe the mod to
  players: whenever one changes, update the other (the long description leaves out building, code and licensing).

## Other mods

The FlexIt mod inspired this one. Never copy code from other mods or the game, and never commit decompiled code.
