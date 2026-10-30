# Building upgrades: findings and open work

Status 2026-10-30, branch `fix/upgrade-deformation` (on `develop` 8cbe44a, 3 commits: Core pieces, integration,
docs). Deployed and tested in game once (car park storey, school extension wing).

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
  building** (still open, see 5.3).
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
- Diagnostics: `Upgrades of building …` lists each installed upgrade (prefab, kind, offset, net/area/lane counts) once
  per building and session.
- In game: the car park storey deforms with the building and **no longer crashes**.

## 3. The crash

Native crash ("Got a UNKNOWN while executing native code", no managed stack, no mod error) a few seconds after
deforming a building with an upgrade.

| Test | Result |
|---|---|
| Car park storey, before this branch | crash |
| Car park storey, with this branch (storey swapped and updated too) | no crash |
| School with classroom wing (upgrade building, untouched by the branch) | crash |
| Same, Settings → attachments: nets and lanes off | still crash; also no "slid to the end of road" warning |

Ruled out: path syncing and road-end sliding. They still ran with nets off (that option only switches prefab net
deformation, see 5.5), but the crash also happened without any road-end slide.

**Likely cause (not yet confirmed):** a swap marks only the owner `Updated`. The game rebuilds the owner's sub-lanes,
so old lane entities are deleted and new ones created. Upgrades keep `LaneConnection` references (start/end lane) to
the owner's old lanes. `LaneConnectionSystem` re-checks only lanes of `Updated` entities (`CheckUpdatedLanesJob`, query
`Updated` + `SubLane`), so the upgrade keeps pointing at deleted entities. Pathfinding then reads garbage, which
crashes natively. This fits all four tests.

**Fix to try first (step 0):**
- `PrefabSwapper.Swap`: after the owner changes prefab, add `Updated` to every `InstalledUpgrade` entity (extensions
  and upgrade buildings, swapped or not, skipping `Deleted`).
- If the swapped entity is itself an upgrade (`Owner` + `Game.Buildings.ServiceUpgrade`, e.g. the wing deformed with
  its own cage), also add `Updated` to its owner.
- Log the number of refreshed upgrades.
- Test: school with the wing (deform, reset, run a few minutes), the wing deformed on its own, the car park again.

If it still crashes: next suspects are the upgrade's clear areas and the terrain lot merge. Add diagnostics first.

## 4. Decided with the user

- Variant identity stays on the **lot box** (names, deform keys, manifest). Saves since 1.0.0 must stay compatible.
- Extensions are deformed by the owner's cage (done). Upgrade buildings with their own lot (the school wing) **keep
  their own cage**: the owner's deformation leaves them alone, and shared paths stay connected as today (nodes shared
  with outside edges never move).
- The cage shown and edited should cover the building plus its deformed extensions, also extensions with their own
  lot outside ("combined, plus outside lots").

## 5. Open work

### 5.1 Combined edit cage (after the crash fix)
- A trilinear cage deformation is multilinear in x, y, z. Continued outside its box (how extensions are baked), it
  equals the trilinear deformation of any larger axis-aligned box whose corners go where the small cage sends them.
  - Edit corner offset = `D_lot(editCorner) − editCorner`; lot corner offset = `D_edit(lotCorner) − lotCorner`.
  - Both conversions evaluate a `TrilinearCageDeformation` and are exact up to 0.1 m quantization.
- Core: `ExtendedCage.Convert(fromBox, fromOffsets, toBox)` + box union, with tests.
- `BuildingDescription.EditBox` = lot box ∪ deformed extensions' boxes. An extension's box is its mesh box
  (`CageBoxMeasurer.TryMeasureMeshes`) moved by `m_Position`; external lots add `m_OverrideLotSize × 8 m` around the
  position. Cache per extension prefab. Share the eligibility check with `PlanExtensionTargets`.
- `DeformService` works in edit space:
  - `TryGetCage` returns `EditBox` and converted specs; `SetOffsets`/`Apply`/`MoveHandle` convert edit → lot, then
    clamp and validate on the lot box.
  - FlexIt import goes through an internal lot-space `ApplyLot`.
  - The UI needs no change: it only uses `state.Box` and state offsets.
- Rounding: a lot → edit → lot round trip can swallow a 0.1 m nudge. Keep the last `(lot, edit)` pair per building in
  `DeformService` and return that edit cage while the lot offsets still match.
- With an external extension, the edit cage's bottom is the footprint of building plus upgrade. Ground fit, align and
  the "leaves its plot" warning then act on that footprint.
- Drop the `m_ExternalLot` skip. Their terrain pad isn't lifted (`LotLiftSystem` only handles the owner's lot):
  document it as a limitation.

### 5.2 Upgrade buildings deformed on their own
They already have their own cage. Check, after step 0, that deforming the wing alone doesn't crash and that its
paths stay connected to the school's.

### 5.3 New upgrades on deformed buildings
The upgrade tool only offers upgrades whose `ServiceUpgradeBuilding` lists the owner's prefab. Options to research:
- Add the owner variant to each fitting upgrade prefab's `ServiceUpgradeBuilding` buffer and copy the base's
  `BuildingUpgradeElement` buffer to the variant.
- Or temporarily swap the building back to its base while the upgrade tool is open.

A newly placed extension on a deformed owner stays undeformed until the next edit (or bake it right away).

### 5.4 Smaller items
- Extensions above the lot box (the storey) are bent by the cage continued upwards. Fine for a storey; very large
  top moves may look odd.
- The `m_Position` placement check skips extensions moved by Move It. Consider using the instance offset (would need
  it in the name).

### 5.5 Found on the way (pre-existing)
- The attachment toggles (Settings) only switch the **prefab** deformation. Area and net **instance** sync
  (`LotInstanceSync`) still runs, so with nets off the saved paths are still moved while the prefab's are not. Either
  gate the instance sync by the same options or document it.

## 6. Test notes
- Logs: `%USERPROFILE%\AppData\LocalLow\Colossal Order\Cities Skylines II\Logs\Unsquare.Mod.log` and `Player.log`.
  No crash dump is written.
- `Testing.md` rows 106–108 cover the car park storey. Add rows for the school wing and the combined cage.
- Build: compile-check from PowerShell (`dotnet msbuild Unsquare/Unsquare.csproj -t:Restore,Compile`; Git Bash broke
  the mscorlib path). Deploy (`dotnet build Unsquare.sln`) only when the user asks.
