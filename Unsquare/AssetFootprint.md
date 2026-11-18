# Asset footprint: shapes in saves and a geometry cache

Handover of 17 Nov 2026 for the sessions that implement it. No code was changed. Findings come from reading
Unsquare, the game's assemblies (decompiled for reading only) and the local `ModsData/Unsquare` folder. Runtime
memory was never measured.

**Decision:**
- **Saves:** they store each deformed building as its base prefab plus a small cage component. Variants are rebuilt
  after load, or taken from cache.
- **Cache:** geometry assets become a cache with a size cap (default 2 GB, an option), evicted at startup by least
  recent use. This replaces the 14-day prune.
- **Old saves:** saves since 1.0.0 are converted on load.

## 1. Measurements and findings

### Disk

`ModsData/Unsquare` on the developer's machine:

| | Assets | Size |
|---|---|---|
| All `.Geometry` files | 1,960 | 684 MB |
| Used by a city (manifest `Cities` not empty) | 526 | 110 MB |
| Used by no city: edit leftovers, 2–10 days old, waiting for the prune | 1,444 | 574 MB |

- **Manifest:** 1,120 entries over 150 bases, about 2.1 assets per entry. The two cities use 81 and 172 variants.
- **Largest:** `FR_Hospital` has 95 variants of about 5 MB each, mostly from test edits.
- **Why:** almost every edit makes a new shape, and each shape is a new variant with its own assets. Undo is the
  only common way back to an earlier shape.

### What costs memory today

- **Every manifest entry is registered on every load:** `VariantSystem.RestoreVariants` (VariantSystem.cs:965)
  creates 1,120 prefabs with their render prefabs, for a city that uses 81. This costs prefab objects, ECS
  entities, load time and other mods' catalog work (Find It, Placemaker's rescan).
- **Meshes are streamed by the game:** `BatchMeshSystem` loads them only for rendered instances, within a
  1.5 GB soft budget. Unseen variants cost no mesh memory. Each visible unique shape is its own mesh; only bones
  would change that (BakeSpeed.md, "Replacing the bake with bones").
- **Source meshes leak for the whole session:** `RenderVariantBuilder.ObtainAsset` calls `source.ObtainMeshes()`
  and never `ReleaseMeshes()`. `GeometryAsset` reference-counts these, so every base mesh and LOD ever baked from
  stays loaded. It stays as full Unity meshes plus raw data, a copy the game's renderer doesn't keep. This explains
  part of `obtain` measuring 0 ms in BakeSpeed.md.
- **Assets are already a cache:**
  - They are keyed by source mesh, building cage and placement (`DeformKey.ForSubmesh`, `AssetNaming`). Variants
    that differ only in lot cage, flat terrain or blend mode share them.
  - `ObtainAsset` rebakes a missing asset (RenderVariantBuilder.cs:88-106).
- **The spec lives only in the manifest:**
  - A variant name is `<Base>__uq` plus 12 hex of a one-way hash (`VariantNaming`). From the name alone only the
    base can be recovered (`TryParse`).
  - At runtime, `PrefabResolver`'s map, filled by bakes and restores, is the truth (`TryDescribe`,
    VariantSystem.cs:335; `TryGetVariantCage`, :303).

## 2. Decision and reasoning

Options considered:

| Option | Outcome |
|---|---|
| Mark entries never in use at a save, drop them at the next start | Dropped for the route below |
| Bones instead of baked meshes | Not now; downsides in BakeSpeed.md |
| Delete leftovers while the game runs | Deferred, see below |
| Base prefab plus cage component in saves, assets as a size-capped cache | **Chosen** |

Why the chosen route:
- **Self-contained saves:** a save carries its shapes and doesn't depend on a manifest on the player's disk.
- **Only what a save uses is registered,** instead of every variant ever made.
- **No usage bookkeeping:** no `Cities` and no age prune for new variants. Disk use is bounded by the cap.
- **Safe mod removal:** the game skips an unknown component with a warning, and buildings load plain. Today they
  become missing-prefab placeholders.

Deleting leftovers while the game runs was deferred:
- **Blocker:** `TryGetOrCreateVariant` reuses a registered prefab by name and returns before checking its asset or
  manifest entry (VariantSystem.cs:466-475). Getting back to a deleted shape would render a variant without
  geometry, and save a building whose variant is in no manifest.
- **Frees disk only:** prefabs stay registered until restart. `PrefabSystem.RemovePrefab` exists but moves the last
  prefab into the freed index, marks the entity `Deleted`, and leaves references dangling: upgrade lists edited by
  `UpgradeOwnership`, tool previews, other mods' catalogs.

## 3. Design

### Component

- **Type:** an `IComponentData, ISerializable` on buildings, in Integration.
- **Unmanaged:** the game's serializers require `unmanaged`, so use fixed fields or a serializable buffer, not
  `int[]`.
- **Contents:** a format version first (the game offers no per-mod version), then lot offsets, building offsets or
  a linked flag, `FlatTerrain` and `BlendMode`. Together that is `DeformSpec`.
- **Conversion** to and from `DeformSpec` in Core, with tests.
- **Identification:** the game writes the type by assembly-qualified name. Renaming it later needs
  `[FormerlySerializedAs]`. An unknown type on load is skipped with a warning.

### Around every save: a plain prefab swap

`GameManager.Save` runs the `Serialize` phase in one frame, for manual saves and autosaves alike. The order is
`BeginPrefabSerializationSystem`, `SerializerSystem`, `EndPrefabSerializationSystem`, `WriteSystem`. The serializer
snapshots entities when it runs.

**Before save:** a system in `SystemUpdatePhase.Serialize`, ordered before `BeginPrefabSerializationSystem`. It must
be before Begin, which decides which prefabs are written; a variant left referenced after Begin would point at an
unwritten prefab. For every building, not Temp or Deleted:
- **On an Unsquare variant:** write or update the component from the resolver's spec. Set `PrefabRef` to the base,
  and the `PrefabRef` of each installed extension on an extension variant to its base. Record the old references.
- **Not on a variant:** remove a stale component, e.g. after a reset.
- **Not done:** no `Updated`, no `PrefabSwapper`, no `LotInstanceSync`. Areas and nets are saved in the variant
  layout as they are.

Writing the component at save time also covers copies (Move It, eyedropper) that never got one.

**After save:** a system after `SerializerSystem` restores the recorded references in the same frame, even if the
save failed. The simulation doesn't run in between.

**Why not a full swap to base:** a round trip through `PrefabSwapper` is not lossless.
- **Nets drift:** they are re-solved by inverting the cage, a Newton solve with 1 mm tolerance. It is deterministic,
  so the error can add up in one direction over many saves.
- **Failed inversions** leave a point in place, and the way back applies the cage a second time.
- **Road-side ends** slide along the road's tangent, which differs between the legs on curved roads.
- **Edited areas** within the 1 m match are reset to the prefab layout.
- **Props and lanes** would be rebuilt twice per save.
- **Level-ups and condemnation:** the level-up hand-off (`LevelUpCarrySystem`) and condemn guard (`CondemnGuardSystem`)
  only act on buildings that are on their variant.

### Load

- **When:** `OnGameLoaded`. Components are read in the `Deserialize` phase, and `ResolvePrefabsSystem` has already
  pointed `PrefabRef` at live prefabs.
- **Variants:** collect the distinct (base, spec) pairs from buildings with the component on their base, and create
  each with `TryGetOrCreateVariant`.
  - **Cache hit** (asset exists and `BoundsCache` has its bounds): no vertex work.
  - **Miss:** a bake of 35–90 ms (BakeSpeed.md).
- **Swap:** queue a swap mode that skips `LotInstanceSync`, since areas and nets are already in the variant layout.
  - It keeps `Updated`, so props and lanes rebuild from the variant.
  - It keeps extension planning (`PlanExtensionTargets`): extension variant names derive from the owner's variant
    name, so extensions need no component.
- **Unchanged:** the extension variant naming, and the resolver map, which is filled by these creations as by bakes
  today.

### Old saves: convert on load

- **Preload restore stays for legacy saves.** Prefabs must be registered before `SerializerSystem` runs in the
  `Deserialize` phase (`PrefabSystem.Deserialize` looks prefab IDs up then). The game has no hook to create an
  unknown prefab on demand; an unknown ID becomes a missing-object placeholder. So the manifest becomes legacy:
  restored at preload as today, frozen (new bakes stop calling `Register`).
- **Conversion happens on the next save.** Buildings on legacy variants keep running on them this session, and the
  next save writes base plus component. Names are unchanged, so a legacy prefab and a new-route variant of the same
  shape are the same prefab.
- **The legacy manifest shrinks by itself:** `MarkUsage` drops a city from entries it no longer uses, and the
  14-day prune removes those entries. The prune stays for legacy entries only; older saves of a city that is
  already converted then break after 14 days, as today.
- **Open:** Unsquare variants on a Placemaker base probably already fail to restore, since Placemaker builds its
  variants after load. Not verified.

### Geometry cache

- **Last-used time** per asset in `BoundsCache` (`bounds.json`, a cache, not save-relevant). Updated when an asset
  is baked or used by a variant created at load. Missing means the file's time.
- **Eviction at startup:** in housekeeping (first preload, before the legacy restore), delete the least recently
  used own `.Geometry` files until the total is at most the cap.
  - Never while the game runs.
  - Never assets claimed by a legacy manifest entry: the legacy restore would rebake them on every load.
- **Option:** `PipelineOptions.GeometryCacheGigabytes`, default 2, settings slider; replaces `PruneUnused` and
  `PruneAfterDays` for new-route assets.
- **Replace `PrunePlanner.PlanOrphanSweep`:** it deletes assets no manifest entry claims, which would be every
  new-route asset (or its 50 % guard would refuse every sweep).
- **Unchanged:** prefab names, deform keys, asset names and GUID seeds.

### Save compatibility

This is the save-format change that CLAUDE.md requires confirmation for. The user confirmed the route, the 2 GB
default and convert on load on 17 Nov 2026. CLAUDE.md's "Lot deformation" and persistence notes need updating with
the implementation.

## 4. Risks and open questions

Verify while implementing:
- **Plain buildings after load:** prefabs created in `OnGameLoaded` need at least one `PrefabUpdate` frame to
  initialise, and swaps wait for `_simulationReady`. Check whether this finishes before the loading screen ends.
- **Frames on the base after load:**
  - `CondemnGuardSystem` only clears `Condemned` on variant buildings.
  - `LevelUpCarrySystem` only carries a level-up from a variant. A building `UnderConstruction` at save time loads
    on its base and would lose its shape if the level-up completes first.
- **Placemaker:** its save systems run in the same phase on its own buildings. Check the ordering and that adopted
  buildings, which are already Unsquare variants, are untouched by it.
- **Load time:** about 6–15 s for 170 uncached variants. The first bake of a session costs 165 ms.
- **Mod removal:** buildings load plain while their areas and paths keep the deformed layout. Acceptable; tell
  players.
- **Legacy restore cost** remains until the legacy manifest has shrunk.

## 5. Implementation order

1. **Component, plain swap around saves, load path.** They ship together, or saves lose shapes. Touches `Mod.cs`
   (system order), `VariantSystem` (load collection, swap mode without lot sync), `PrefabSwapper`, and new systems
   in `Integration/Systems`. Core: spec ↔ component conversion with tests.
2. **Legacy freeze and convert on load.** `VariantSystem` (`Register` only for legacy), `VariantManifest`.
3. **Geometry cache.** `BoundsCache` (last-used), a Core eviction planner with tests replacing the orphan sweep,
   `VariantHousekeeper`, `PipelineOptions`, `UnsquareSettings`, `LocaleEN`.
4. **Release source meshes.** Independent and small: `ReleaseMeshes()` in a `finally` in `ObtainAsset`, or a small
   size-limited source cache that releases on eviction (BakeSpeed.md item 3).
5. **Later:** sub-meshes whose part of the cage moves rigidly reuse the base mesh. Measure memory with snapshots
   before and after.

`Testing.md` rows to add with step 1–3:
- save and load
- autosave while deformed
- mod removed
- 1.0.x save converted
- cache eviction over the cap
- level-up across a save
- Placemaker row saved
