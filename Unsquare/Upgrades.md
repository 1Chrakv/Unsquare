# Building upgrades: findings and open work

Status 2026-10-31, branch `fix/upgrade-deformation` (on `develop` 8cbe44a). The upgrade crash is fixed and confirmed
in game (section 3), and so is the combined cage over on-lot upgrades (5.1). Picking upgrade buildings inside
Unsquare mode (5.3) is confirmed too, as are deformed buildings keeping and gaining upgrades (5.4) and upgrades and
names in the info panel (5.7). Open: a car park can get more than one storey (5.8). 5.2 waits for a real building
that needs it.

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

- **Extensions on the owner's lot** (storey, hospital wards, helipad) are baked with the owner's cage in their own
  frame and swapped together with it (`VariantSystem.PlanExtensionTargets`, `TranslatedDeformation`,
  `VariantPrefabFactory.TryCreate(..., frameOrigin, ...)`).
  - Their nets sync together with the owner's, each with its own remap (`NetOwner`, `ExtensionSwap`,
    `NetInstanceSync.Sync(owners)`); their areas sync with their own transform.
  - Name `VariantNaming.ExtensionVariantName` (seed `Unsquare:extension:v1:` + owner variant name). The owner's
    manifest entry lists them in `Extensions` and holds their assets (no FormatVersion bump; 1.0.0 ignores the
    field). They are restored after the owner.
  - Skipped: `m_ExternalLot` (5.2), extensions without meshes or attachments, extensions moved off their prefab place.
- **One cage over the building and its on-lot extensions** (5.1): `ExtendedCage`, `BuildingDescription.EditBox`;
  `DeformService` converts to the lot cage.
- **Upgrade buildings** (school wings) have their own cage and can be picked in Unsquare mode
  (`RaycastFlags.SubBuildings`, 5.3).
- **Crash fix:** variant prefabs get the base's upgrade buffers in their instance archetype (`UpgradeArchetype`,
  section 3).
- **Info panel and menus** (5.7):
  - upgrade variants keep a group-less `UIObject` and drop their unlock requirements;
  - every variant aliases its base's locale keys (`VariantLocalization`);
  - `UpgradeRegistration` keeps cloned upgrades out of upgrade menus.
- **Upgrades on deformed buildings** (5.4): variants share the base's upgrade list and are accepted as owner
  (`UpgradeOwnership`). `ExtensionInstallSystem` bakes a new on-lot extension at once.
- All prefab setup after init runs in `VariantSystem.PrepareVariantPrefab`.
- **Diagnostics:** `Upgrades of building …` lists each installed upgrade (prefab, kind, offset, net/area/lane counts)
  once per building and session.

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

### 5.1 One cage for the building and its on-lot upgrades (done, confirmed in game 2026-10-31)
- Only extensions on the owner's lot count (the same set `PlanExtensionTargets` deforms with the owner). Extensions
  with their own lot keep their own cage (5.2).
- A trilinear cage deformation is multilinear in x, y, z. Continued outside its box (how extensions are baked), it
  equals the trilinear deformation of any larger axis-aligned box whose corners go where the small cage sends them.
  - Edit corner offset = `D_lot(editCorner) − editCorner`; lot corner offset = `D_edit(lotCorner) − lotCorner`.
  - Both conversions evaluate a `TrilinearCageDeformation` (`ExtendedCage.Convert`) and are exact up to the 0.1 m
    grid. Lot → edit → lot always gives the lot offsets back (tested).
- `BuildingDescription.EditBox` = the lot box with its top raised (`ExtendedCage.RaiseTop`) to the highest deformed
  on-lot extension: its mesh box top (`CageBoxMeasurer.TryMeasureMeshes`) plus `m_Position.y`, cached per extension
  prefab (`VariantSystem.DeformedExtensionsTop`).
- **Scope: only the top is raised.** x/z stay the lot's, like the rule that the cage is the lot rectangle (overhangs
  aren't enclosed). The bottom corners equal the lot's, so ground fit, align, the "leaves its plot" warning and
  neighbours' lot outlines are unchanged.
- `DeformService` works in edit space:
  - `TryGetCage` returns `EditBox` with converted specs.
  - `Apply` (and with it `SetOffsets`, `MoveHandle`, `Reset`, undo) converts edit → lot, then clamps and validates on
    the lot box.
  - FlexIt import uses the private lot-space `ApplyLot`.
  - The UI needed no change.
- Rounding: edit → lot → edit can swallow a 0.1 m nudge of a top corner. `DeformService` keeps the last
  `(edit box, lot, edit)` per building and returns those edit offsets while the lot offsets (target or applied) still
  match.

### 5.2 Extensions with their own lot: own cage
**No real case found yet (2026-10-31):** the `Upgrades of building …` log lines so far show only extensions on the
owner's lot and upgrade buildings:
- `Hospital01`: Trauma Center, Special Ward, Helipad, all `external False` at position 0, deformed fine with the
  hospital's cage.
- `ParkingHall03`: Additional Floors, `external False`.
- `ElementarySchool02`: Classroom Extension Building and Gymnasium, both upgrade buildings (5.3).

Do this step only once a log line shows `external True`.

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

### 5.3 Upgrade buildings deformed on their own: picking in Unsquare mode (done, confirmed in game 2026-10-31)
**Works already (tested 2026-10-31 with the school's classroom wing):** an upgrade building has its own cage and
deforms like any building. Selecting the wing with the game's default tool and then switching to Unsquare mode keeps
it as the target (`UnsquareToolSystem.OnStartRunning` → `TryResolveBuilding(m_ToolSystem.selected)`), and it
deforms fine.

**Doesn't work:** clicking the wing **inside** Unsquare mode targets the school instead.

Cause (verified in the game code):
- The game's raycast walks a hit up the `Owner` chain: object hits in `Game.Objects.RaycastJobs.ValidateResult`, lot
  hits in `Game.Areas.RaycastJobs`.
- It stops at an upgrade building only if the raycast has `RaycastFlags.SubBuildings`: `ServiceUpgrade` +
  (`Building` or a net hit).
- The default tool sets that flag outside the editor (`DefaultToolSystem.InitializeRaycast`); the Unsquare tool
  doesn't (`UnsquareToolSystem.InitializeRaycast` sets only `RaycastFlags.BuildingLots`). So the hit continues to
  the wing's owner, the school. `TryResolveBuilding` itself would stop at the wing, since it has `Building`.

Fix (commit 56c620b): `UnsquareToolSystem.InitializeRaycast` adds `RaycastFlags.SubBuildings`.

To check in game (`Testing.md` rows 117–119):
- Hovering (dim cage) and clicking now pick the wing.
- The school is still picked when clicking the school, its lot or an on-lot extension like the car park storey.
  Extensions have no `Building`, so the walk goes on to the owner.
- Deforming the wing alone doesn't crash (also when hovering the wing and the school).
- Its paths stay connected to the school's.
- Reset, undo and reload work.

### 5.4 New upgrades on deformed buildings (done, confirmed in game 2026-10-31)
Tested (`Testing.md` 122, 123):
- The deformed school's upgrade menu offers the usual upgrades.
- A storey added to a deformed car park was swapped to the deformed storey right away. The log shows
  `New upgrade on deformed building …` and `Lot sync … 1 extensions`; there's no `Bake` line because the extension
  variant already existed.
- The same worked for a new hospital extension.
- Side effect found: more than one storey can be added (5.8).

Found together with 5.7: once the main building is deformed, its info panel hides the whole Upgrades section, even
for undeformed upgrades. `UpgradesSection.Visible` needs a `BuildingUpgradeElement` buffer on the building's prefab.
The upgrade menu (`UpgradeMenuUISystem`) uses the same buffer, and placement only accepts an owner prefab listed in the
upgrade's `ServiceUpgradeBuilding` (`ObjectToolBaseSystem`).

Fix (`UpgradeOwnership.Share`, called in `VariantSystem.ProcessSimData`):
- Copy the base's `BuildingUpgradeElement` list to the variant, leaving out upgrade variants.
- Append the variant to each of those upgrades' `ServiceUpgradeBuilding`. `BuildingInitializeSystem` and the
  extractor check in `PrefabUISystem` read only the first entry, so appending is safe.

New on-lot extensions on a deformed building are baked at once:
- `ExtensionInstallSystem` (after `ModificationEndBarrier`) sees `Extension` + `ServiceUpgrade` + `Owner` +
  `Created` and calls `VariantSystem.RequestExtensionRefresh`.
- The next prefab update queues a swap to the owner's current variant with `PlanExtensionTargets`. Only the new
  extension changes; its nets are remapped from undeformed to the applied cage.
- New upgrade buildings with their own lot stay undeformed (own cage).

### 5.5 Smaller items
- The `m_Position` placement check skips extensions moved by Move It. Consider using the instance offset (would need
  it in the name).

### 5.6 Found on the way (pre-existing)
- The attachment toggles (Settings) only switch the **prefab** deformation. Area and net **instance** sync
  (`LotInstanceSync`) still runs, so with nets off the saved paths are still moved while the prefab's are not. Either
  gate the instance sync by the same options or document it.

### 5.7 Upgrades and names in the info panel (done, confirmed in game 2026-10-31: `Testing.md` 120–122)
**Seen in game (school with several wings):**
- The info panel listed only the undeformed wing under Upgrades → Sub-buildings.
- A deformed school's title read `Assets.NAME[ElementarySchool02__uq…]`.
- The upkeep lines of deformed wings read just "Maintenance".
- With the school itself deformed, the Upgrades section was gone entirely (5.4).

**Missing upgrades:**
- Cause: `UpgradesSection` lists an installed upgrade only if its prefab has `UIObjectData`.
  `VariantPrefabFactory` removed `UIObject` from every clone to keep variants out of the build menus.
- Fix (`VariantPrefabFactory.KeepOutOfMenus`): upgrade variants keep an owned `UIObject` with `m_Group = null`.
  - `UIObject.LateInitialize` then writes `UIObjectData` with no group and doesn't add the prefab to any menu's
    `UIGroupElement`. The icon stays.
  - They also drop `Unlockable` and `ManualUnlockable`. Otherwise `MilestoneUISystem.m_UnlockableAssetQuery`
    (`UIObjectData` + `ServiceObjectData` + `UnlockRequirement`) would list them in milestone screens.
  - Other variants still drop `UIObject`.

**Names:**
- Cause: `NameSystem.GetId` and `UpkeepSection` use `PrefabUISystem.GetTitleAndDescription`, keyed by prefab name
  (`Assets.NAME/DESCRIPTION[…]`, `Assets.UPGRADE_NAME/UPGRADE_DESCRIPTION[…]` for upgrades). No locale has entries for
  variant names. Growables are named via their zone and were fine.
- Fix: `VariantLocalization` adds aliases (`VariantLocaleKeys`) from the variant's keys to the base's texts in the
  active dictionary.
  - Done for every variant `VariantSystem` creates, finds or restores, and again on `onActiveDictionaryChanged`
    (language switch).
  - Prefabs with their own `Localization` component keep it in the clone.

**To check in game (`Testing.md` rows 120–125):**
- The deformed school shows its normal name and the Upgrades section, with all wings (deformed or not) under their
  names and named upkeep lines. Same for the hospital's and car park's deformed extensions.
- No variant appears in build menus, the upgrade menu, Find It or milestone screens.
- Names stay normal after a language switch.

### 5.8 More than one storey on a deformed car park (found 2026-10-31, not fixed)
**Seen in game:** on a deformed `ParkingHall03`, Additional Floors can be placed several times. The log showed five
`New upgrade on deformed building` lines for one car park. Without Unsquare only one storey is allowed.

**Cause (from the game code):**
- Extensions and upgrades with `ServiceUpgradeData.m_ForbidMultiple` may be installed only once
  (`UpgradeMenuUISystem`: `BuildingExtensionData` or `m_ForbidMultiple`, ~line 200).
- The check for "already installed" compares each installed upgrade's `PrefabRef` with the upgrade prefab:
  `UpgradeMenuUISystem` ~line 355, `component.m_Prefab == upgradeEntity`. `ObjectToolSystem.ForbidMultipleUpgrades`
  (~line 3597) likely checks the same way.
- A deformed storey's prefab is a variant, so the game sees no storey installed and offers it again.
- All storeys of one building get the same extension variant: same owner variant, same extension, same name. They
  sit in the same place.

**Fix ideas (to research):**
- Keep installed extensions on a prefab the check recognizes. Not possible while each deformed storey needs its own
  baked meshes.
- Patch the two checks to compare base prefabs (variant → base by `VariantNaming.TryParse`). That needs a Harmony
  dependency, which the project doesn't use yet; ask first.
- Hide or lock the already-installed upgrade for that building some other way. The menu reads the owner prefab's
  `BuildingUpgradeElement`, which variants share across buildings, so a per-building filter there isn't possible.
- Upgrade buildings with their own lot (school wings) may be placed several times anyway; only extensions and
  `m_ForbidMultiple` upgrades are affected.

## 6. Test notes
- Logs: `%USERPROFILE%\AppData\LocalLow\Colossal Order\Cities Skylines II\Logs\Unsquare.Mod.log` and
  `Player.log`. No crash dump is written.
- `Testing.md` rows:
  - 106–108: car park storey;
  - 109–112: school wing and the hover crash;
  - 113–116: combined cage;
  - 117–119: picking upgrade buildings;
  - 120–125: info panel, menus and new upgrades.
- Build: compile-check from PowerShell (`dotnet msbuild Unsquare/Unsquare.csproj -t:Restore,Compile`; Git Bash broke
  the mscorlib path). Deploy (`dotnet build Unsquare.sln`) only when the user asks.
