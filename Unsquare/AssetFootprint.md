# Asset footprint: shapes in saves and a geometry cache

Handover of 17 Nov 2026 for the sessions that implement it. Findings come from reading Unsquare, the game's
assemblies (decompiled for reading only) and the local `ModsData/Unsquare` folder. Runtime memory was never measured.

**Status 18 Nov 2026:**
- Step 1 (PR A, branch `feature/saved-cages`) is implemented and partly tested in game; see sections 6 and 7.
- Steps 2–4 (PR B, PR C) are open. Section 8 is the handover for PR B.

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

- **When:** in `VariantSystem`'s first update after `OnGameLoaded`, never in `OnGameLoaded` itself. By then
  components are read in the `Deserialize` phase, and `ResolvePrefabsSystem` has already pointed `PrefabRef` at live
  prefabs. Prefabs added during `OnGameLoaded` crash the game; see 7.1.
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
- **Verified 18 Nov:** Unsquare variants on a Placemaker base fail to restore, since Placemaker builds its variants
  after load. The local manifest has 5 such entries (`Placemaker Shape EU_ResidentialMediumRow01_L3_1x5 …__uq…`);
  they are counted under `failed` without a log line. The save systems leave such buildings on their variant
  (`IForeignPrefabs.IsRuntimePrefab`), so they stay legacy.

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

Verify while implementing (results from PR A in 7):
- **Plain buildings after load:** variants created after load need at least one `PrefabUpdate` frame to
  initialise, and swaps wait for `_simulationReady`. Verified: swaps run in the first frame after the loading
  screen. Creating them in `OnGameLoaded` itself crashed the game (7.1).
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

## 6. PR A as implemented (`feature/saved-cages`)

Commits `42e04e5`..`f102779` on top of `develop` (ac9e592). Changes from the design above:

- **Component** `Integration/Saves/SavedCage`. Its namespace and type name are part of the save format.
  - It holds `int Format` plus `FixedList128Bytes<short> Values`. The game's collections library has no 256-byte
    list, and shorts fit cage steps up to ±3 km. Values are written to the save as ints, so the memory layout can
    change freely.
  - `[FormerlySerializedAs("Unsquare.Integration.Saves.SavedCage, Unsquare")]` sits on the type itself, so a new
    assembly version still resolves; the game trims the stored name down to that form.
  - The Core codec `SavedCageCodec` (v1: flags, blend mode, lot cage, and the building cage unless linked) has 12
    tests.
- **Save:**
  - `SavedCageWriteSystem` runs before `BeginPrefabSerializationSystem`, and `SavedCageRestoreSystem` after
    `SerializerSystem`.
  - The ledger lives in `VariantSystem.SaveSwaps`. A restore that never ran is redone in `VariantSystem.OnUpdate`.
  - Structural changes are batched, and stale cages are removed through a `SavedCage` query.
- **Buildings whose base can't be saved stay on their variant:** an unknown spec, a base that is itself a variant,
  or a Placemaker runtime prefab (`IForeignPrefabs.IsRuntimePrefab`). This is logged once per prefab.
- **Load:**
  - `OnGameLoaded` only sets `_savedCagesPending`. `RestoreSavedCages` runs in the next `OnUpdate` (PrefabUpdate,
    before `PrefabInitializeSystem`).
  - It creates each (base, spec) once with `VariantOrigin.Load` and queues the swaps with `syncLot: false` and
    `ExpectedCurrent` set to the base entity.
  - A building that is on another prefab by then, because a level-up finished first (same lot size), goes to
    `PrepareLevelUp`. Any other prefab drops the swap with a warning.
  - `TrySwap` refuses targets without an instance archetype (`Not swapping to …: the game did not initialize it.`),
    and `VariantArchetype` warns when it meets one (`… has no instance archetype`).
- **`VariantOrigin`** replaces `isRestore`: Edit is validated; Load and Legacy are never refused.
- **Manifest bridge (PR A only):** Load still calls `_manifest.Register`, and `CollectUsedVariantKeys` counts
  `SavedCage` buildings, so the orphan sweep and `MarkUsage` keep the new-format variants. PR B removes both.
- **`PrefabSwapper.Swap(..., syncLot)`** skips `LotInstanceSync` on load. `LevelUpCarrySystem.SameLot` is now shared.
- **Docs:** the CLAUDE.md "Saves" paragraph and compatibility rule, the README (mod removal, downgrade), and
  Testing.md rows 188–198.

## 7. Findings from in-game testing (18 Nov 2026)

### 7.1 Variants created in `OnGameLoaded` crash the game

- **Symptom:** a native crash ("Got a UNKNOWN while executing native code", no managed stack) in the first frame
  after loading, right after the load swaps. It only happened when the variants did not exist yet, i.e. without a
  manifest.
- **Cause:**
  - `PrefabInitializeSystem` only initialises prefabs that have `Created` and an enabled `PrefabData`.
  - Prefabs added in `OnGameLoaded` lose `Created` during the rest of the load, before that system runs, so they
    stay uninitialised and their `ObjectData.m_Archetype` is invalid.
  - Archetype completion then does nothing ("0 archetypes completed"), and buildings on those prefabs crash the
    game's jobs.
- **Fix:** create the variants in the next `VariantSystem.OnUpdate`, as edits do, and refuse swaps to uninitialised
  prefabs. Afterwards all archetypes were valid, 32 of 43 were completed, and there was no crash (Testing row 198).

### 7.2 Saves hold no variant prefab records

- **The concern:** the game might still save variant `PrefabID`s for their unlock or budget state
  (`CheckSavedPrefabsJob`).
- **The result:** loading a new-format save without Unsquare logged 0 `Unknown prefab ID` lines and one
  `Not serializable type: Unsquare.Integration.Saves.SavedCage, …` line. Buildings came back plain with the
  deformed lot, as designed (row 191).
- **Consequence:** no diagnostic for saved variant records is needed.

### 7.3 Old saves depend on the manifest; never delete it

- **What happened:**
  - Testing rows 192 and 198 first said to delete `variants.json`. After that was done several times, the manifest
    held only the entries used since.
  - An old save ("Testfield 23") then loaded with 159 white boxes (`Unknown prefab ID …__uq…`).
- **Repair:** the last full manifest (the backup, 1,128 entries) was merged with the current one (268 newer
  entries) into 1,396 entries. The next load logged `Restored 1388 variants (+73 upgrade extensions), 8 failed` and
  0 unknown IDs.
- **The rows now** move the files aside and back. `.bak` must be moved too, because `AtomicJsonFile` falls back to
  it.
- **For PR B:** consider a never-rewritten copy of the frozen manifest, and restoring from it when `variants.json`
  is missing (section 8).

### 7.4 Other observations

- **Load cost of the legacy restore:** 47 s of loading for 1,396 entries, in a city that uses about 160. The
  new-format load of 40 cached variants took 1.5–2 s.
- **8 legacy restore failures:**
  - 3 `FR_Hospital` entries with the retired `BlendMode` 1 (`ArgumentOutOfRangeException` in
    `VariantNaming.BlendSuffix`). A separate fix has been proposed.
  - The 5 Placemaker-base entries (section 3).
- **Orphan sweep refusals:**
  - With a partial manifest the sweep refused every time ("Would delete 1964 of 1964", "Entry … lists no assets").
  - Load-registered entries of variants with undeformed meshes (e.g. `CityPark09`) list no assets and block it.
  - PR B replaces the sweep anyway.
- **Disk today:** 2,671 `.Geometry` files, 961 MB in `ModsData/Unsquare` (1,960 files and 684 MB on 17 Nov).
- **Paradox Mods connecting late at boot:**
  - When it connects after boot (`Defer Mods load`), the packs are enabled about a minute later.
  - A save loaded before that shows missing-content dialogs and grey boxes.
  - Wait in the menu. This is unrelated to Unsquare.
- **Backups belong outside the game's data folder:**
  - A copy inside it was scanned by the game, which saw a second `Unsquare.coc` and logged errors at shutdown.
  - The developer backup is in `C:\Users\Tobi\Unsquare-Backup-2026-11-18`.

### 7.5 Testing status (rows in Testing.md)

| Row | Result |
|---|---|
| 191 mod removed | passed |
| 192 1.1.0 save converted, loaded without manifest | passed |
| 198 no-manifest load (the crash case) | passed after the fix |
| 188–190, 193–197 | open |

## 8. Handover for PR B (steps 2 and 3)

- **Base:** branch `feature/geometry-cache` off `feature/saved-cages` while PR A is not merged, or off `develop` once
  it is. Steps 2 and 3 ship together: freezing without replacing the orphan sweep would delete every new asset.
- **Remove the PR A bridge:**
  - Load no longer calls `_manifest.Register` (`TryGetOrCreateVariant`), and Edit stops too.
  - `AddExtension` is called only for `VariantOrigin.Legacy`. Thread the origin through `PlanExtensionTargets` →
    `TryGetOrCreateExtensionVariant`.
  - `CollectUsedVariantKeys` drops its `SavedCage` loop.
- **Decided with the user (17 Nov):**
  - The cap defaults to 2 GB, with a slider from 0.5 to 20 GB.
  - `PruneUnused` and `PruneAfterDays` are removed from the settings.
  - Legacy entries are pruned after a fixed 14 unused days, and the assets of pruned entries are not deleted but
    fall to the cache.
- **Design details:** section 3 "Geometry cache" and the plan in 8.1.
- **Proposed, needs the user's decision:** a write-once copy of the frozen manifest (e.g.
  `variants.legacy-backup.json`) that preload restores from when `variants.json` and `.bak` are both missing (7.3).
- **Test without risk:** move manifest files aside, never delete them. Keep the developer backup until the user
  confirms that PR B works.
- **Questions:** this document and the code should answer them. The PR A session ("AssetFootprint implementation
  plan", Claude Desktop) has a long context and is expensive to wake; ask it only about history that is in neither.

### 8.1 Implementation plan

**Freeze the manifest (step 2)**, in `Integration/Systems/VariantSystem.cs`:
- `TryGetOrCreateVariant`: only `VariantOrigin.Legacy` touches the manifest, through `MergeAssets` as today. Edit and
  Load build no `ManifestEntry` and call no `Register`.
- `TryGetOrCreateExtensionVariant`: give it the origin and call `_manifest.AddExtension` only for Legacy. Pass the
  origin through `PlanExtensionTargets` from:
  - `Bake` (Edit)
  - `ProcessLevelUps` (Edit)
  - `ProcessExtensionRefreshes` (Edit)
  - `Request` for identity (Edit)
  - `ResetAll` (Edit)
  - `RestoreSavedCages` (Load)
  - `RestoreExtensions` (Legacy)
- `CollectUsedVariantKeys`: remove the `SavedCage` loop. `MarkUsage` then drops converted cities from legacy entries
  by itself.
- `VariantManifest` keeps its format (`FormatVersion` 3, no bump). Add a class remark that it is legacy and frozen.

**Bounds cache with last use**, in `Core/Persistence/BoundsCache.cs` (`bounds.json`, a cache, not save-relevant):
- **Format:** an optional 7th float in each array: last use in Unix days (resolution about 3 min). The loader
  accepts 6 or 7 elements; an entry without it counts as never touched. Today's loader drops arrays whose length
  isn't 6, so an older mod version would re-measure bounds. That only costs time.
- **New members:**
  - `Touch(string assetName, DateTime nowUtc)`: no-op for unknown assets. It marks the cache dirty only when the
    stored value is more than 1 h older, so `bounds.json` isn't rewritten after every edit.
  - `bool TryGetLastUsed(string, out DateTime)`.
- **Tests** in `Unsquare.Tests/Persistence/BoundsCacheTests.cs`:
  - round trip with last use
  - a 6-element entry has no last use
  - touching an unknown asset does nothing and stays clean
  - a touch within the hour stays clean, one after it is dirty
- **Callers:** `Integration/Prefabs/RenderVariantBuilder.ObtainAsset` calls `Touch` on a hit and after a bake. That
  covers edits, load creations and legacy restores.

**Eviction planner**, new `Core/Housekeeping/CacheEvictionPlanner.cs` plus a `CachedAsset` readonly struct:
- **Input:** `CachedAsset(string Name, long Bytes, DateTime LastUsedUtc)`. The caller passes the file's write time
  when `bounds.json` has no last use.
- **`IReadOnlyList<string> Plan(IReadOnlyList<CachedAsset> assets, ISet<string> claimed, long capBytes)`:**
  - The total counts every asset, claimed or not.
  - It deletes the least recently used unclaimed assets (ties by name, ordinal) until the total is at most the
    cap.
  - If claimed assets alone exceed the cap, it returns every unclaimed asset.
  - It throws `ArgumentException` for a cap ≤ 0.
- **Tests** in `Unsquare.Tests/Housekeeping/CacheEvictionPlannerTests.cs`:
  - under the cap → nothing
  - over the cap → oldest first, stops as soon as it fits
  - claimed assets are never returned
  - claimed alone over the cap → all unclaimed
  - deterministic ties
- **Removals:** `PrunePlanner.PlanOrphanSweep`, `OrphanSweepPlan.cs`, the sweep constants and their tests in
  `Unsquare.Tests/Housekeeping/PrunePlannerTests.cs`. `PlanPrune` stays for legacy entries; keep its tests.

**Asset store and housekeeping:**
- `Integration/Assets/GeometryAssetStore.cs`: new `ListOwnAssets()` returning name, bytes and write time.
  - Enumerate `*.Geometry` in `ModPaths.DataDirectory` with `FileInfo`, filtered by `AssetNaming.IsOwnAssetName` on
    the name without extension. The `.cid` sidecars are tiny; ignore them.
  - Deletion stays `Delete(names)`, through the asset database.
- `Integration/Systems/VariantHousekeeper.Run(manifest, bounds, nowUtc, capBytes)`:
  - First the legacy prune with `PrunePlanner.DefaultMaxUnusedAge` (14 days): remove the expired entries from the
    manifest. Do not delete their assets; they fall to the cache.
  - Then eviction. `claimed` is the union of all remaining entries' `Assets`, and the last use comes from
    `bounds.TryGetLastUsed`, else the file time.
  - Delete the planned names, remove them from `BoundsCache`, and log the count and the MB freed against the total.
- `VariantSystem.RunHousekeeping` passes `_options.GeometryCacheBytes`. It still runs only on the first preload,
  before `RestoreVariants`, and only when `LoadState` says the manifest is trusted.

**Options and settings:**
- `Core/Options/PipelineOptions.cs`:
  - Replace the constructor parameters `pruneUnused` and `pruneAfterDays` with `float geometryCacheGigabytes`. The
    default is 2, and a value ≤ 0 throws.
  - Add `GeometryCacheBytes`.
  - Remove `PruneUnused`, `PruneAfterDays` and `PruneAfter`.
  - Update `ToString` and `Unsquare.Tests/Options/PipelineOptionsTests.cs`.
- `UI/Input/UnsquareSettings.Tunables.cs`:
  - Remove `PruneUnused`, `PruneAfterDays` and `IsPruneDisabled`.
  - Add `GeometryCacheGigabytes` in `PerformanceGroup` with `[SettingsUISlider(min = 0.5f, max = 20f, step = 0.5f,
    unit = Unit.kFloatSingleFraction)]`. `Clamped` needs the slider attribute.
  - Update `ToPipelineOptions` and `SetSettingsDefaults`. Players' saved values for the removed settings are simply
    ignored.
- `LocaleEN.cs`: replace the two prune entries with "Geometry cache size (GB)" and the description "At startup,
  deletes the least recently used deformed geometry beyond this size; it is rebuilt when needed."

**Optional, only if the user agrees:** the write-once manifest copy (section 8). Write `variants.legacy-backup.json`
once, when it doesn't exist yet. If `variants.json` and `.bak` are both missing at preload, `LoadState` restores
from it and logs that.

**Docs:**
- CLAUDE.md: in the options line, "pruning" becomes "geometry cache size". The "Saves" paragraph says that the
  manifest is legacy and frozen.
- HowTo.md: assets are a cache.
- This document: mark steps 2 and 3 as done.
- README: mention the cache size option if it lists options.
- Testing.md rows:
  - Lower the slider to 0.5 GB and restart: the log names the deleted count and MB, and the folder is about
    0.5 GB plus the legacy-claimed assets.
  - Then load a save whose shapes were evicted: they are rebaked, not lost.
  - Old save with a manifest: its assets survive eviction.
  - New edits add no manifest entries (compare the `variants.json` entry count before and after).

**Verify:** `dotnet test Unsquare.Tests`, then a compile check with
`dotnet msbuild Unsquare/Unsquare.csproj -t:Compile`. Ask before any deploying build. Back up `ModsData/Unsquare`
outside the game folder before the first in-game run with eviction.
