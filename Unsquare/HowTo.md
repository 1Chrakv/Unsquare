# How Unsquare bakes deformed buildings

Every building of a prefab shares its meshes, so one building can't simply be bent in place. Unsquare gives a
deformed building its own **variant prefab**: a clone of the base prefab whose meshes are deformed by the cage,
baked into geometry assets and restored in every later session. This document describes that pipeline and the
game behaviour it relies on. Type names refer to this repository; game API names match CS2 1.x.

---

## 0. Overview

```
Cage edit ─▶ IBuildingDeformService ─▶ VariantSystem (building, DeformSpec)
               │ variant prefab exists? ──yes──▶ (wait until initialized) ─▶ PrefabSwapper
               │ no
               ▼
          Debouncer (PipelineOptions.RebakeDelaySeconds) ─▶ VariantPrefabFactory:
              clone prefab, RenderVariantBuilder deforms meshes + LODs, GeometryAssetStore saves them,
              attachment deformers place props/areas/lanes/nets, AddPrefab, manifest entry
          ─▶ wait until the prefab entity loses `Created` ─▶ PrepareVariantPrefab ─▶ PrefabSwapper
Next session: OnGamePreload ─▶ VariantManifest ─▶ re-create every variant (assets + BoundsCache reused)
              OnGameLoadingComplete ─▶ PrepareVariantPrefab ─▶ mark used variants ─▶ PrunePlanner ─▶ VariantHousekeeper

PrepareVariantPrefab = SimDataCopier + upgrade setup (UpgradeRegistration, UpgradeArchetype, UpgradeOwnership).
On-lot upgrade extensions get their own variants with the owner's cage and swap with the owner; the UI edits a
cage raised over them (ExtendedCage). Details: Upgrades.md.
```

`VariantSystem` runs in `PrefabUpdate`, before `PrefabInitializeSystem` (`Mod.OnLoad`), so prefabs it adds are
initialized in the same frame. It disables itself while no bake, swap or copy is queued.

---

## 1. Identity: names, keys, GUIDs

Saves reference prefabs by name, so every name must be deterministic and must never change silently
(see `CLAUDE.md`, "Prefab names, deform keys ...").

* **Deformation identity:** `DeformSpec` = cage corner offsets in grid steps (`CageOffsets`) + `FlatTerrain`.
* **Variant prefab name** (`VariantNaming`): `<BaseName>__uq<hash>`, hash of
  `Unsquare:lot-offsets:v1:` + canonical offsets (+ `|flat-terrain`). `TryParse` reverses it, so a
  building already on a variant maps back to its base. Always deform from the base, never from a variant.
* **Deform key** (`DeformKey`): cage box + offsets + `FormatVersion`; `ForSubmesh` adds the submesh placement,
  because one render prefab can be placed differently in different buildings.
* **Upgrade extension variants** (`VariantNaming.ExtensionVariantName`): `<ExtensionName>__uq<hash>`, hash of
  `Unsquare:extension:v1:` + the owner's variant name. They are baked with the owner's cage, moved into the
  extension's frame by its prefab position, and listed in the owner's manifest entry (`Extensions`).
* **Geometry assets** (`AssetNaming`): name `UQ_` + 16 hex and GUID, both from `StableHash` of
  `Unsquare:` + render prefab name + `:` + deform key. The prefix makes own files recognisable for the sweep.

---

## 2. Base prefab and cage box

`PrefabResolver` finds prefabs by `PrefabID`; during preload, prefabs that aren't indexed yet are found through
`PrefabSystem`'s prefab list (reflection, wrapped in try/catch). `CageBoxMeasurer` measures the lot box
(the cage) and the building's mesh box (facade alignment for props), see `CLAUDE.md`, "Lot deformation".

---

## 3. Deforming meshes

The deformation is a pure function in building space (`TrilinearCageDeformation`, `IDeformation`): x = width,
y = up, z = depth. `VertexDeformer` maps each vertex submesh-local → building space → deformed → back
(`SubmeshTransform`). Normals use the inverse-transpose Jacobian, tangents the Jacobian itself (handedness kept).

**Packed vertex data:** game meshes store normals octahedrally as `SNorm16 × 2` and tangents as one `Float32`
holding two 15-bit octahedral values plus the handedness in the sign bit (`_TANGENTSPACE_OCTO`).
`mesh.normals`/`mesh.tangents` can't read them and their setters change the layout the shader decodes.
`MeshDeformer` reads and writes them raw (`PackedVertexChannel`) and converts with `OctahedralPacking`.

**Meshes are shared:** always `Object.Instantiate` before editing and `Object.Destroy` every temporary mesh.

**LODs:** `RenderVariantBuilder` deforms every `LodProperties.m_LodMeshes` entry the same way; otherwise the
original shape pops back at a distance. The coarsest LOD (~500 triangles) only moves at its vertices, so a
twisted cage makes walls poke through roofs. When `CageTwist.MaxResidualMeters` (distance from the best affine
fit) exceeds `PipelineOptions.CoarseLodTwistMeters`, the clone leaves that LOD out and draws the next finer one
to the culling distance. Affine cages keep it: moving vertices reproduces them exactly.

Deformed render prefabs are cached per asset name; many building prefabs share render prefabs.

---

## 4. Geometry assets

`GeometryAssetStore` reuses an existing asset by GUID or creates it with
`AssetDatabase.user.AddAsset<GeometryAsset, Mesh[]>` under `ModsData/Unsquare`, saves it and unloads it (the game
reloads on demand). Assets land in `%LocalLow%\Colossal Order\Cities Skylines II\ModsData\Unsquare\`.
`BoundsCache` (`bounds.json`) stores each asset's bounds, so restoring variants at preload needs no vertex work.

---

## 5. Variant prefab

`VariantPrefabFactory` clones the base prefab (`PrefabCloning` guards against shared mutable state) and:

* points each `m_Meshes` entry at its deformed render prefab;
* removes `SpawnableBuilding` (the zone spawner must never pick a variant), `ObsoleteIdentifiers` (would alias
  the base's old IDs) and `UIObject` (keeps it out of build menus);
* puts it in the editor category `Unsquare/Variants` and excludes it from Find It;
* deforms attachments (`IAttachmentDeformer`: props, areas, lanes, nets), see `CLAUDE.md`;
* registers it with `PrefabSystem.AddPrefab` and adds a manifest entry.

---

## 6. Initialization, simulation data, swap

A new prefab entity keeps `Created` until `PrefabInitializeSystem` has filled its data, so `VariantSystem`
queues the copy and the swap until it is gone and the simulation is ready.

* `SimDataCopier`: removing `SpawnableBuilding` also drops the zone's prefab data, so `SpawnableBuildingData`,
  `BuildingPropertyData`, `ConsumptionData`, `PollutionData`, `OfficeBuilding` and `LevelUpResourceData` are copied
  from the base when missing. Without them a zoned building loses residents, jobs, level, upkeep, its address and
  most info panel sections. `BuildingSpawnGroupData` is never copied (spawner). It also copies the zone flags
  of `ObjectGeometryData`: initialized without `SpawnableBuilding`, a variant gets `OverrideZone` and would wipe the
  zoning under its lot.
* The copy runs for every created variant, including restored ones (`OnGameLoadingComplete`): saved buildings
  already reference their variant, so no swap would trigger it.
* `PrefabSwapper` sets the building's `PrefabRef`, adds `Updated` (simulation re-evaluates) and `BatchesUpdated`
  (renderer rebuilds batches), and rewrites saved area and net instances (`LotInstanceSync`). A zero cage swaps
  back to the base prefab.

---

## 7. Persistence

Saves store each building's `PrefabRef` by name; variant prefabs exist only in memory. So every variant must be
registered again before the save's entities are deserialized:

* `VariantManifest` (`variants.json`) lists every variant (`ManifestEntry`: base type/name, variant name,
  offsets, `FlatTerrain`, cities, last use, asset names), keyed `BaseType/VariantName`.
* `OnGamePreload` re-creates every entry; existing assets and cached bounds make this cheap.
* Writes go through `AtomicJsonFile` (`.tmp` → replace, `.bak` fallback). An unreadable manifest is moved aside
  (`.corrupt-<time>`), never treated as empty. `FormatVersion` mismatches still drop old entries;
  the next format change must add a migration instead (saves are compatible since 1.0.0).

---

## 8. Housekeeping

In `OnGameLoadingComplete`, `VariantSystem` collects the variants used by this city (`CityConfigurationSystem.cityName`)
and `PrunePlanner` decides:

* used here: add the city, update `LastUsedUtc`; unused here: remove the city;
* used by no city for `PipelineOptions.PruneAfterDays` (default 14, option): prune the entry and delete its
  assets, except assets still claimed by another entry;
* orphan sweep (`OrphanSweepPlan`): delete `UQ_` assets no entry claims, but never more than half of the own
  files once over 10 (`MaxSweepShare`, `SweepGuardMinCount`), so a naming bug can't wipe everything.

`VariantHousekeeper` executes the plan. Renaming a city, or two cities with the same name, confuses the tracking.

---

## 9. Pitfalls

* **Saves depend on the mod:** a save with variant buildings needs the mod, the manifest and the assets.
* **Names are permanent:** changing naming, prefixes, deform-key format or GUID seeds orphans every variant.
  Bump the version parts instead, and only after confirmation.
* **Submeshes** with `DefaultMissingMesh` or stacked meshes may need special handling.
* **Main thread:** baking is synchronous `Mesh` work; the debounce keeps repeated nudges to one bake.
* **Game updates:** wrap every game-API step (clone, `AddPrefab`, asset I/O, reflection) in try/catch, log, and
  degrade by not swapping.

In-game test steps: `Testing.md`.
