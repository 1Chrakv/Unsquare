# Building upgrades: findings and open work

Status 2026-10-31, branch `fix/upgrade-deformation` (on `develop` 8cbe44a). The upgrade crash is fixed and confirmed
in game (section 3). Open work is in section 5.

## 1. How CS2 models upgrades (from the decompiled `Game.dll`)

Decompile with `ilspycmd -p -o <scratchpad>/game -r <Managed> <Managed>/Game.dll` (see memory `game-decompiler`);
never commit decompiled code. Type names below are the game's.

An upgrade is its own entity: `Owner` = main building, `Game.Buildings.ServiceUpgrade` tag, listed in the building's
`InstalledUpgrade` buffer (`ServiceUpgradeReferencesSystem` keeps that buffer). There are two kinds:

| Kind | Prefab | Entity | Placement | Example |
|---|---|---|---|---|
| Extension | `BuildingExtensionPrefab` (an `ObjectGeometryPrefab`) with `ServiceUpgrade` | `Extension`, no `Building` | Fixed: owner transform + prefab `m_Position` (same rotation); `m_ExternalLot` = own lot outside the owner's | `ParkingHall03` extra storey |
| Upgrade building | `BuildingPrefab` with `ServiceUpgrade` (gets `PlaceableObjectData`) | `Building` with its own lot | Player-placed within `m_MaxPlacementDistance`, any rotation | `ElementarySchool02 Classroom Extension Building`, logged at (24, 0, −60) from the school |

- The game never moves or rebuilds an upgrade when only its owner changes. `SubObjectSystem` skips
  `ServiceUpgrade`/`Building` sub-objects. So a prefab swap of the owner leaves upgrades exactly as they were.
- `ServiceUpgrade.m_Buildings` lists the building prefabs an upgrade fits. `ServiceUpgrade.LateInitialize` adds it
  to each one's `BuildingUpgradeElement` buffer (the upgrade menu) and fills the upgrade's `ServiceUpgradeBuilding`
  buffer. `BuildingInitializeSystem` derives lot-less extensions' bounds and lot from `ServiceUpgradeBuilding`, so
  `m_Buildings` must not be emptied on a clone.
- An owner variant is not in any upgrade's `m_Buildings`, which is why **new upgrades can't be added to a deformed
  building** (still open, see 5.4).
- Upgrades interact with the owner through: clear areas (`ClearAreaHelpers.FillClearAreas`, used by `SubObjectSystem`
  and `LaneSystem`), the terrain lot (`BuildingUtils.CalculateLotInfo` merges non-external extension flat areas),
  parking connections (`LaneConnectionSystem.FindParkingConnection` searches owner and upgrade lanes) and combined
  stats (`UpgradeUtils`).

## 2. What the branch does now

- Extensions (not upgrade buildings) are baked with the owner's cage in their own frame and swapped together with
  the owner (`VariantSystem.PlanExtensionTargets`, `TranslatedDeformation`, `VariantPrefabFactory.TryCreate(...,
  frameOrigin, ...)`).
- Their nets are synced together with the owner's, each with its own remap (`NetOwner`, `ExtensionSwap`,
  `NetInstanceSync.Sync(owners)`), so shared nodes stay connected. Their areas are synced with their own transform.
- Name: `VariantNaming.ExtensionVariantName` = `<Ext>__uq<hash>`, seed `Unsquare:extension:v1:` + owner variant name.
  The owner's manifest entry lists `Extensions` ("Type/BaseName") and holds their assets (`VariantManifest.AddExtension`,
  no FormatVersion bump; 1.0.0 ignores the field). Restored after the owner (`RestoreExtensions`).
- Skipped: `m_ExternalLot`, extensions without meshes or attachments, extensions moved off their prefab place
  (`InstalledExtensions.IsAtPrefabPlacement`).
- `UpgradeRegistration.RemoveFromBuildings` (after prefab init) removes a cloned upgrade from its buildings' upgrade
  menus. This also covers stand-alone upgrade buildings deformed with their own cage.
- `UpgradeArchetype.AddUpgradeBuffers` gives variant prefabs the upgrade buffers of their base's instance archetype.
  This is the crash fix, see section 3.
- Diagnostics: `Upgrades of building …` lists each installed upgrade (prefab, kind, offset, net/area/lane counts) once
  per building and session.
- In game: the car park storey deforms with the building; no crash when hovering deformed buildings with upgrades.

## 3. The crash (fixed)

Native crash ("Got a UNKNOWN while executing native code", no managed stack, no mod error) a few seconds after
deforming a building with an upgrade.

| Test | Result |
|---|---|
| Car park storey, before this branch | crash |
| Car park storey, storey deformed with the building | seemed fine, but the car park wasn't hovered; it crashes on hover too |
| School with classroom wing (upgrade building) | crash |
| Same, Settings → attachments: nets and lanes off | still crash; also no "slid to the end of road" warning |
| School with wing, with the upgrade group refresh (see "Disproved" below) | still crash |
| ElementarySchool02 without the wing: deform, reset, deform again | no crash |
| Paused: add the wing, deform the school, Alt+T (flat terrain) at once | crash **while still paused** |
| Paused, after deforming: stay in Unsquare mode, leave it looking away, then hover the school, then click | no crash until the mouse **hovers the school** with the game's default tool |
| Only the top face moved up (ground paths unchanged), then hover | crash: the path junction is not the cause |
| With `UpgradeArchetype` (school with wing, car park with storey), hover and click | no crash |

**Cause (found 2026-10-31 from the game code, confirmed in game):**
1. A building prefab's instance archetype (`ObjectData.m_Archetype`) gets `InstalledUpgrade`, `Game.Net.SubNet` and
   `SubRoute` only because upgrades register with it. `ServiceUpgrade.LateInitialize` → `BuildingPrefab.AddUpgrade`
   adds a `BuildingUpgradeElement` buffer → `BuildingPrefab.RefreshArchetype` adds those types when that buffer
   exists.
2. A variant clone is in no upgrade's `m_Buildings`, so it has no `BuildingUpgradeElement` and its archetype lacks
   `InstalledUpgrade`. Existing instances keep their own archetype, so nothing shows at first.
3. Hovering makes the default tool (`DefaultToolSystem.CreateDefinitionsJob`) create temporary copies of the building
   and its upgrades. `GenerateObjectsSystem` creates the building copy from the **variant's** archetype, so the copy
   has no `InstalledUpgrade` buffer.
4. `ServiceUpgradeReferencesSystem.UpdateUpgradeReferencesJob` (Burst) then does
   `m_InstalledUpgrades[owner.m_Owner]` for the temporary upgrade, without a check, on that owner copy. That is an
   invalid buffer access, and the game crashes natively.

This fits every test: no upgrade → no temporary upgrade → no crash; crash only on hover, paused or not; terrain, nets
and lanes don't matter.

**Fix (`UpgradeArchetype.AddUpgradeBuffers`, called after prefab init and on swap; confirmed in game 2026-10-31):**
- After a variant prefab is initialized (next to `SimDataCopier`), if its base has a `BuildingUpgradeElement`
  buffer, rebuild the variant's `ObjectData.m_Archetype` with `InstalledUpgrade`, `Game.Net.SubNet` and `SubRoute`
  added (the same types `RefreshArchetype` adds).
- Copying the base's `BuildingUpgradeElement` buffer would also do it, but would offer upgrades in the variant's
  upgrade menu. That belongs to 5.4 and needs `ServiceUpgradeBuilding` too.
- `EntityManager.CreateArchetype` is called by reflection: a direct call also binds its `ReadOnlySpan` overload,
  which the game's mscorlib doesn't provide (CS0518).
- The log line `Copied sim data to N variants (M given upgrade buffers)` shows how many variants got them.

**Disproved on the way** (kept so they aren't retried):
- **Path syncing and road-end sliding:** it crashed with nets off and without any road-end slide.
- **Simulation (pathfinding, services):** it crashed while paused.
- **Our terrain lift:** it crashed on the flat-terrain variant too.
- **Stale `LaneConnection` references:** the theory was that a swap rebuilds the owner's lanes while upgrades keep
  references to the deleted ones (`LaneConnectionSystem.CheckUpdatedLanesJob` only re-checks `Updated` entities).
  The fix tried for it marked the whole upgrade group `Updated` on every swap (`PrefabSwapper.RefreshUpgradeGroup`).
  It didn't help and was removed.
- **Path junction between the school and the wing:** it crashed with ground paths unchanged.
- **"The car park stopped crashing once its storey was a variant":** it had only not been hovered.

## 4. Decided with the user

- Variant identity stays on the **lot box** (names, deform keys, manifest). Saves since 1.0.0 must stay compatible.
- **Upgrades on the owner's lot** (extensions without `m_ExternalLot`, e.g. another storey) are treated as if they
  were one building with it: deformed by the owner's cage (done), and **one cage encloses the base and these
  upgrades** (5.1).
- **Upgrades with their own lot** are deformed **separately from their main building**, with their own cage box,
  name and manifest entry. The owner's deformation leaves them alone, and shared paths stay connected as today
  (nodes shared with outside edges never move). This covers:
  - Upgrade buildings (the school wing): they already have their own cage (5.3).
  - Extensions with `m_ExternalLot`: they don't have one yet (5.2).
  (Replaces the earlier decision "combined, plus outside lots" from 2026-10-30.)

## 5. Open work

### 5.1 One cage for the building and its on-lot upgrades (after the crash fix)
- Only extensions on the owner's lot count (the same set `PlanExtensionTargets` deforms with the owner). Extensions
  with their own lot keep their own cage (5.2).
- A trilinear cage deformation is multilinear in x, y, z. Continued outside its box (how extensions are baked), it
  equals the trilinear deformation of any larger axis-aligned box whose corners go where the small cage sends them.
  - Edit corner offset = `D_lot(editCorner) − editCorner`; lot corner offset = `D_edit(lotCorner) − lotCorner`.
  - Both conversions evaluate a `TrilinearCageDeformation` and are exact up to 0.1 m quantization.
- Core: `ExtendedCage.Convert(fromBox, fromOffsets, toBox)` + box union, with tests.
- `BuildingDescription.EditBox` = lot box ∪ the on-lot extensions' boxes. An extension's box is its mesh box
  (`CageBoxMeasurer.TryMeasureMeshes`) moved by `m_Position`. Cache per extension prefab. Share the eligibility check
  with `PlanExtensionTargets`.
- `DeformService` works in edit space:
  - `TryGetCage` returns `EditBox` and converted specs; `SetOffsets`/`Apply`/`MoveHandle` convert edit → lot, then
    clamp and validate on the lot box.
  - FlexIt import goes through an internal lot-space `ApplyLot`.
  - The UI needs no change: it only uses `state.Box` and state offsets.
- Rounding: a lot → edit → lot round trip can swallow a 0.1 m nudge. Keep the last `(lot, edit)` pair per building in
  `DeformService` and return that edit cage while the lot offsets still match.
- On-lot extensions stay inside the lot rectangle, so the edit cage normally only grows upwards (a storey). Its
  bottom corners then equal the lot's, and ground fit, align and the "leaves its plot" warning behave as today. If
  an on-lot extension's mesh sticks out sideways, the cage grows there too, and those tools act on the larger
  footprint.

### 5.2 Extensions with their own lot: own cage
Extensions with `m_ExternalLot` are deformed separately, like a building. They stay skipped by the owner's
deformation (`IsDeformable` in `VariantSystem` already excludes them). What they still need:
- **Cage box:** `CageBoxMeasurer.TryMeasureLot` only knows `BuildingPrefab`. Add `BuildingExtensionPrefab` with
  `m_ExternalLot`: lot rectangle `m_OverrideLotSize × 8 m` around the extension's origin (as
  `BuildingInitializeSystem` lays out its lot), from the ground to its roof.
- **Describe and pick:**
  - `VariantSystem.TryDescribe` requires `Building`; also accept such extension entities.
  - Check that the Unsquare tool's lot raycast hits extension lots (research; otherwise pick via the extension's
    geometry).
- **Identity:** a normal variant, `VariantNaming.VariantName(extensionBase, offsets, flat)`, with its own manifest
  entry (`BaseType` `BuildingExtensionPrefab`); the owner isn't involved. Restore already handles any
  `ObjectGeometryPrefab`.
- **Swap:** the normal path (`PrefabSwapper`, its own areas and nets). Shared nodes with the owner's nets stay fixed.
- **Terrain lift:** `LotLiftSystem` only handles building variants. Either extend it to extension lots (the game
  terraforms external extension lots on their own, see `TerrainSystem` and `BuildingUtils.CalculateLotInfo`), or
  keep their terrain flat and document it.
- **Upgrade menu:** a cloned extension registers itself as an upgrade option; `UpgradeRegistration` already removes
  that.

### 5.3 Upgrade buildings deformed on their own
They already have their own cage. Check that deforming the wing alone doesn't crash (also when hovering the wing
and the school) and that its paths stay connected to the school's.

### 5.4 New upgrades on deformed buildings
The upgrade tool only offers upgrades whose `ServiceUpgradeBuilding` lists the owner's prefab. Options to research:
- Add the owner variant to each fitting upgrade prefab's `ServiceUpgradeBuilding` buffer and copy the base's
  `BuildingUpgradeElement` buffer to the variant.
- Or temporarily swap the building back to its base while the upgrade tool is open.

A newly placed extension on a deformed owner stays undeformed until the next edit (or bake it right away).

### 5.5 Smaller items
- Extensions above the lot box (the storey) are bent by the cage continued upwards. Fine for a storey; very large
  top moves may look odd.
- The `m_Position` placement check skips extensions moved by Move It. Consider using the instance offset (would need
  it in the name).

### 5.6 Found on the way (pre-existing)
- The attachment toggles (Settings) only switch the **prefab** deformation. Area and net **instance** sync
  (`LotInstanceSync`) still runs, so with nets off the saved paths are still moved while the prefab's are not. Either
  gate the instance sync by the same options or document it.

## 6. Test notes
- Logs: `%USERPROFILE%\AppData\LocalLow\Colossal Order\Cities Skylines II\Logs\Unsquare.Mod.log` and `Player.log`.
  No crash dump is written.
- `Testing.md` rows 106–108 cover the car park storey. Add rows for the school wing, the combined cage and own-lot extensions.
- Build: compile-check from PowerShell (`dotnet msbuild Unsquare/Unsquare.csproj -t:Restore,Compile`; Git Bash broke
  the mscorlib path). Deploy (`dotnet build Unsquare.sln`) only when the user asks.
