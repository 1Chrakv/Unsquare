# Asset footprint: shapes in saves and a geometry cache

Design of 17 Nov 2026 and its implementation on `feature/asset-saving-redesign`. Findings come from reading Unsquare,
the game's assemblies (decompiled for reading only) and the local `ModsData/Unsquare` folder. Runtime memory was never
measured.

**Status 22 Nov 2026:** steps 1–4 (saved cages, frozen manifest, geometry cache, old-saves popup, source mesh release)
are implemented (section 5). Saved cages are partly tested in game (6, 7.1); the rest is not. Next steps: 7.2.

**Decision:**
- **Saves:** they store each deformed building as its base prefab plus a small cage component. Variants are rebuilt
  after load, or taken from cache.
- **Cache:** geometry assets become a cache with a size cap (default 2 GB, an option), evicted at startup by least
  recent use. This replaces the 14-day prune.
- **Old saves:** saves since 1.0.0 keep loading through the frozen manifest and convert at their next save.

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
  part of `obtain` measuring 0 ms in BakeSpeed.md. Fixed by step 4 (5.5).
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
  prefabs. Prefabs added during `OnGameLoaded` crash the game; see 6.1.
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

Verify while implementing (results in 6):
- **Plain buildings after load:** variants created after load need at least one `PrefabUpdate` frame to
  initialise, and swaps wait for `_simulationReady`. Verified: swaps run in the first frame after the loading
  screen. Creating them in `OnGameLoaded` itself crashed the game (6.1).
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

## 5. As built

Commits `42e04e5`..`f102779` (step 1, "PR A"), `a495fe4`..`ae79fa0` (steps 2–3 and the popup, "PR B" and "B2"),
`11c685f` (step 4). Decided with the user: the route and the 2 GB default (17 Nov); the cap counts unclaimed assets
only, the safety net is an append-only archive with two Settings buttons, and the legacy prune stays at a fixed 14
days (19 Nov).

### 5.1 Migration of existing saves

| Save | References | Load path |
|---|---|---|
| 1.0–1.1 (legacy) | variant `PrefabID`s | preload restores manifest entries (`VariantOrigin.Legacy`); converted at the next save over the same file |
| 1.2+ | base + `SavedCage` | `RestoreSavedCages` after load (`VariantOrigin.Load`); no manifest |
| 1.2+ leftovers | a few variant IDs (Placemaker-base variants) | like legacy; a warning is logged when such a variant has no manifest entry |

Lifecycle of a legacy city:
1. Preload restores its entries. The manifest is frozen, so restores only complete existing entries (`MergeAssets`,
   `AddExtension`).
2. Load complete: `MarkUsage` marks the city.
3. The next save writes base + cage; names are unchanged.
4. On the next load of that save, buildings are still on their bases at load complete, so the city drops out of its
   entries.
5. 14 days later the entries are pruned. Their assets become cache, and the entries stay in the archive.

Kept invariants:
- **Formats:** the manifest format is unchanged (`FormatVersion` 3; a 1.1 downgrade reads it). `bounds.json` takes 6
  or 7 floats; an older mod version drops 7-float entries and only re-measures them.
- **Eviction never loses a shape:** it runs only at the first preload, before anything is created, and a missing asset
  is rebaked.
- **Corrupt archive:** a corrupt `variants.archive.json` is never overwritten.

### 5.2 Saved cages (step 1)

- **Component** `Integration/Saves/SavedCage`. Its namespace and type name are part of the save format.
  - It holds `int Format` plus `FixedList128Bytes<short> Values`. The game's collections library has no 256-byte
    list, and shorts fit cage steps up to ±3 km. Values are written to the save as ints, so the memory layout can
    change freely.
  - `[FormerlySerializedAs("Unsquare.Integration.Saves.SavedCage, Unsquare")]` sits on the type itself, so a new
    assembly version still resolves; the game trims the stored name down to that form.
  - The Core codec `SavedCageCodec` (v1: flags, blend mode, lot cage, and the building cage unless linked) is tested.
    `SavedCages` converts between component and `DeformSpec` and reads the base and spec a building was saved with.
- **Save:**
  - `SavedCageWriteSystem` runs before `BeginPrefabSerializationSystem`, and `SavedCageRestoreSystem` after
    `SerializerSystem`.
  - The ledger lives in `VariantSystem.SaveSwaps`. A restore that never ran is redone in `VariantSystem.OnUpdate`.
  - Structural changes are batched, and stale cages are removed through a `SavedCage` query.
- **Buildings whose base can't be saved stay on their variant:** an unknown spec, a base that is itself a variant,
  or a Placemaker runtime prefab (`IForeignPrefabs.IsRuntimePrefab`). This is logged once per prefab.
- **Load** (`VariantSystem.Saves.cs`):
  - `OnGameLoaded` only sets `_savedCagesPending`. `RestoreSavedCages` runs in the next `OnUpdate` (PrefabUpdate,
    before `PrefabInitializeSystem`).
  - It creates each (base, spec) once with `VariantOrigin.Load` and queues the swaps with `syncLot: false` and
    `ExpectedCurrent` set to the base entity.
  - A building that is on another prefab by then, because a level-up finished first (same lot size), goes to
    `PrepareLevelUp`. Any other prefab drops the swap with a warning.
  - `TrySwap` refuses targets without an instance archetype (`Not swapping to …: the game did not initialize it.`),
    and `VariantArchetype` warns when it meets one (`… has no instance archetype`).
- **`VariantOrigin`:** Edit is validated; Load and Legacy are never refused.
- **`PrefabSwapper.Swap(..., syncLot)`** skips `LotInstanceSync` on load. `LevelUpCarrySystem.SameLot` is shared.

### 5.3 Frozen manifest, archive and geometry cache (steps 2–3)

- **Frozen manifest:** Edit and Load register no entries. Extension variants pass their origin, and only Legacy
  calls `AddExtension`. `CollectUsedVariantKeys` counts only buildings on variants. Every legacy restore failure is
  logged and kept for the Retire check.
- **Archive** (`variants.archive.json`, manifest format). `Integration/Saves/LegacyManifestStore` owns manifest and
  archive:
  - Every first preload appends new manifest entries (`VariantManifest.AddMissing`) before the prune. It is
    append-only rather than write-once, so it also catches entries a 1.1 downgrade adds.
  - When the manifest is missing or corrupt, it is rebuilt from the archive (`Revive`: last use now, no cities). That
    session is untrusted, so no housekeeping runs.
- **Settings group "Saves from before version 1.2":**
  - *I've converted all my old saves* (`IBuildingDeformService.RetireLegacyManifest`): archives the manifest and
    writes it empty, not deleted, since a missing file would be rebuilt from the archive. It is refused while the
    loaded city has entries that failed to restore.
  - *Restore shapes for old saves* (`RestoreLegacyShapes`): revives archived entries missing from the manifest. They
    load with the next save opened.
- **`BoundsCache`:** an optional 7th float holds the last use in Unix days (about 3 min resolution). `Touch` comes
  from `RenderVariantBuilder.ObtainAsset` on a hit and after a bake (edits, load creations and legacy restores); it
  marks the file dirty only when the stored use is over an hour old.
- **`CacheEvictionPlanner`** (Core, tested) replaces the orphan sweep (`OrphanSweepPlan` removed). The least recently
  used unclaimed assets go (ties by name) until the unclaimed total is at most the cap. Claimed (legacy) assets are
  neither counted nor evicted, so a large legacy set can't force every new asset to be rebaked at each start.
- **`VariantHousekeeper`** prunes legacy entries (assets kept) and lists own `.Geometry` files from the folder
  (`GeometryAssetStore.ListOwnAssets`; last use from `bounds.json`, else the file time). It evicts beyond
  `PipelineOptions.GeometryCacheGigabytes` (slider 0.5–20 GB, default 2) through the asset database, drops bounds of
  deleted assets, and logs the MB.
- **Settings removed:** `PruneUnused` and `PruneAfterDays`; players' saved values for them are ignored.

### 5.4 Old-saves popup

One popup at load, no second step after saving (autosaves don't convert the original file).
`VariantSystem.CountLegacyBuildings` counts buildings on convertible variants at load complete (new-format buildings
are still on their bases then) and raises `LegacySaveLoaded`. `LegacySavesUISystem` opens `legacy-saves-dialog.tsx`
unless the hidden setting `HideLegacySavesPrompt` is set (*Reset settings* clears it). The retire runs from an inline
confirm; its result or refusal is shown in the popup.

### 5.5 Source mesh release (step 4)

- **The leak, confirmed in the game assemblies:**
  - `GeometryAsset.ObtainMeshes` loads meshes and raw data once and counts a reference. `ReleaseMeshes` frees
    them at 0.
  - `ObtainAsset` obtained on every miss and never released.
  - Baked assets were never affected: `GeometryAssetStore.Create` unloads them.
- **`LruBudget<TKey>`** (`Core/Caching`, tested) orders keys by last use with a cost each. The most recent key is
  never evicted.
- **`SourceMeshCache`** (`Integration/Meshes`):
  - It holds one reference per base render prefab, costed by its vertex count.
  - It releases the least recently used sources beyond `PipelineOptions.SourceMeshBudgetVertices`. The default is
    1,000,000, with no slider.
  - It re-obtains a source whose meshes were destroyed.
- **Where it releases everything:** after the preload restore (which also covers leaving a city), after the
  saved-cage restore at load, and in `OnDestroy`.
- **Log:** the bake line shows `sources held N (M vertices)`. Each full release logs `Released source meshes …`.
- **Not measured yet:** the memory saved and the right budget. Rows 213–216 give the first numbers; tune the default
  from them.

### 5.6 Side effects

- **Restore cost:** *Restore shapes for old saves* brings back the full legacy restore for loads (about 47 s per
  1,400 entries) plus 35–90 ms per evicted asset. The restored prefabs show up in other mods' catalogs again.
- **Rebakes:** rebaked assets use the current bake code and attachment options.
- **Claimed assets:** revived entries claim their assets, which then stay outside the cap until pruned again.
- **No-manifest tests:** they must move `variants.archive.json` aside too (Testing rows 192 and 198).
- **Downgrade to 1.1:** 1.1 ignores the archive. Its orphan sweep may delete new or archived-only assets, or refuse
  at 50 %, which only costs rebakes. New-format saves load undeformed.
- **Converted cities:** older 1.x saves of a converted city lose their shapes 14 days after the converted save is
  loaded, until *Restore shapes for old saves* is pressed (README, prune log line).

## 6. Findings from in-game testing (18 Nov 2026)

### 6.1 Variants created in `OnGameLoaded` crash the game

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

### 6.2 Saves hold no variant prefab records

- **The concern:** the game might still save variant `PrefabID`s for their unlock or budget state
  (`CheckSavedPrefabsJob`).
- **The result:** loading a new-format save without Unsquare logged 0 `Unknown prefab ID` lines and one
  `Not serializable type: Unsquare.Integration.Saves.SavedCage, …` line. Buildings came back plain with the
  deformed lot, as designed (row 191).
- **Consequence:** no diagnostic for saved variant records is needed.

### 6.3 Old saves depend on the manifest; never delete it

- **What happened:**
  - Testing rows 192 and 198 first said to delete `variants.json`. After that was done several times, the manifest
    held only the entries used since.
  - An old save ("Testfield 23") then loaded with 159 white boxes (`Unknown prefab ID …__uq…`).
- **Repair:** the last full manifest (the backup, 1,128 entries) was merged with the current one (268 newer
  entries) into 1,396 entries. The next load logged `Restored 1388 variants (+73 upgrade extensions), 8 failed` and
  0 unknown IDs.
- **The rows now** move the files aside and back. `.bak` must be moved too, because `AtomicJsonFile` falls back to
  it.
- **Consequence:** the append-only archive that rebuilds a missing manifest (5.3).

### 6.4 Other observations

- **Load cost of the legacy restore:** 47 s of loading for 1,396 entries, in a city that uses about 160. The
  new-format load of 40 cached variants took 1.5–2 s.
- **8 legacy restore failures:**
  - 3 `FR_Hospital` entries with the retired `BlendMode` 1 (`ArgumentOutOfRangeException` in
    `VariantNaming.BlendSuffix`). A separate fix has been proposed.
  - The 5 Placemaker-base entries (section 3).
- **Orphan sweep refusals** (before step 3 replaced the sweep): with a partial manifest it refused every time ("Would
  delete 1964 of 1964", "Entry … lists no assets"); load-registered entries of variants with undeformed meshes (e.g.
  `CityPark09`) list no assets and blocked it.
- **Disk:** 2,671 `.Geometry` files, 961 MB in `ModsData/Unsquare` (1,960 files and 684 MB on 17 Nov).
- **Paradox Mods connecting late at boot:**
  - When it connects after boot (`Defer Mods load`), the packs are enabled about a minute later.
  - A save loaded before that shows missing-content dialogs and grey boxes.
  - Wait in the menu. This is unrelated to Unsquare.
- **Backups belong outside the game's data folder:**
  - A copy inside it was scanned by the game, which saw a second `Unsquare.coc` and logged errors at shutdown.
  - The developer backup is in `C:\Users\Tobi\Unsquare-Backup-2026-11-18`.

## 7. Testing status and next steps

### 7.1 Testing status (rows in Testing.md)

| Row | Result |
|---|---|
| 191 mod removed | passed |
| 192 1.1.0 save converted, loaded without manifest | passed (before the archive; rerun) |
| 198 no-manifest load (the crash case) | passed after the fix (before the archive; rerun) |
| 188–190, 193–197 | open; some normal behaviour was checked on 22 Nov, record which rows that covered |
| 199–212 (archive, eviction, rebake after eviction, frozen manifest, converted city, archive rebuild, Retire, Restore, Retire refusal, popup) | open |
| 213–217 (source meshes within the budget, no reload per nudge, plain buildings after a release, release on city change, bake time) | open |

Verified without the game: `dotnet test Unsquare.Tests`, `dotnet msbuild Unsquare/Unsquare.csproj -t:Compile`,
`tsc --noEmit` in `UiModule`.

### 7.2 Next steps

1. **Back up first:** copy `ModsData/Unsquare` outside the game folder before the first run with this build. The first
   start writes the archive and evicts cache files beyond the cap. Keep `C:\Users\Tobi\Unsquare-Backup-2026-11-18`
   until the branch is confirmed.
2. **Deploying build:** `dotnet build Unsquare.sln`, only with the user's go-ahead. Never pass `SkipUiModule`.
3. **In-game tests** (all features together) of the open rows in 7.1; rows 192 and 198 now move
   `variants.archive.json` aside too. What to watch for:
   - the housekeeping log line (MB figures plausible, nothing claimed evicted);
   - no `Unknown prefab ID …__uq…` for old saves while the manifest or archive exists;
   - a same-session rebake after eviction (GUID reuse) without asset errors;
   - the popup's count against the log line `N deformed buildings were loaded in the old save format`.
4. **Record results** in 7.1, and fix what the tests find.
5. **Merge:** a PR from `feature/asset-saving-redesign` into `develop` (never `master`), no version bump on the
   branch. Then the `develop` → `master` release bumps the version. The save format changed, so it is a minor bump
   (1.2.0) if the user agrees.
6. **Before release:**
   - Check that the README and the `LongDescription` in `Properties/PublishConfiguration.xml` agree. The long
     description may need one plain sentence that old saves convert when saved over, and that geometry is a size-capped
     cache.
   - Release notes should tell players to open and save over each old save, mention the popup and the two Settings
     buttons, and say that a downgrade to 1.1 loads new-format saves undeformed.
7. **Later (step 5):** reuse base meshes for rigidly moved sub-meshes, and measure memory with snapshots before and
   after.

## 8. Known limits

- **Same-name cities:** legacy usage tracking is per city name, so two cities with the same name keep each other's
  entries alive.
- **Popup:** it appears at every load of a not-yet-converted old save until *Don't show again*. It can't tell
  whether the player later saved over the same file or under a new name.
- **Retire:** it trusts the player's confirmation. *Restore shapes for old saves* is the way back, and costs one slow
  load.
- **Local `FR_Hospital` entries:** the 3 entries with `BlendMode` 1 exist only on the developer's machine. They keep
  failing until pruned, and don't block Retire unless the loaded city uses them.
- **In-session prefabs:** every edited shape still registers a variant prefab until restart (section 2, "Deleting
  leftovers while the game runs"). Long edit sessions accumulate them; this needs its own design.
- **Extensions of unsavable owners:** `SavedCageWriteSystem` moves every extension on an extension variant to its
  base, even when its owner stays on its variant (unsavable base). After load such an extension is plain. Rare: row
  houses, the usual unsavable case, have no extensions.
