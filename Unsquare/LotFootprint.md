# Deforming the lot footprint: findings (October 2026)

**Goal:** when a lot cage is deformed in xz, the terrain under the building should follow the deformed outline.
Today the game keeps levelling the original lot rectangle, which leaves a plateau with a cliff along the old
outline.

**Result:** not solved. The experiments are on branch `feature/lot-footprint-pad` (0.11.1–0.11.2), not merged.

## How the game shapes the ground under a building

Sources: reading Game.dll. Type references are to that assembly.

| Data | Shape | Notes |
|---|---|---|
| `BuildingData.m_LotSize`, `ObjectGeometryData.m_Bounds` | rectangle | Collision (`ObjectUtils.CalculateBaseCorners`), zone blocking (`CellBlockJobs`, `CellOccupyJobs`) and the lot outline (`BuildingLotRenderSystem.DrawLot`). |
| `Game.Buildings.Lot` | 4 edges × 3 heights | Heights relative to the pad, along the **lot rectangle** edges. |
| `BuildingTerraformData.m_FlatX0/Z0/X1/Z1` | octagon | Flat pad: 4 axis-parallel edges (`.y`) plus 4 free corner edges. |
| `BuildingTerraformData.m_Smooth` | rectangle (lot space) | Region where the lot raises and lowers terrain. |
| `AdditionalBuildingTerraformElement` | axis-aligned rectangle or circle | Extra lot copies whose flat area is clipped to the main pad. |
| `TerrainSystem.BuildingLotDraw` | rotated rectangle | Orthonormal axes and no matrix; the `BuildingLot` shader is compiled, so its code can't be read. |

- **Flat pad (octagon).**
  - Points in lot-local xz: `(X0.x,Z0.y) (X0.y,Z0.x) (X0.y,Z1.x) (X0.z,Z1.y) (X1.z,Z1.y) (X1.y,Z1.z) (X1.y,Z0.z) (X1.x,Z0.y)`.
  - `BuildingUtils.CalculateLotInfo` copies it, and `BuildingUtils.SampleHeight` (CPU) and TerrainSystem (GPU)
    both read it the same way.
  - A flat edge outside the lot extents makes `SampleHeight`'s ramp collapse: the lot is then flat right up to
    its edge.
  - `BuildingInitializeSystem.InitializeTerraformData` computes it from the mesh bounds (the leg area for
    buildings on legs), with 1 m corner cuts.
- **Lot edge heights come only from neighbours.** `LotHeightSystem.UpdateLotHeightsJob` (around L616–758) samples
  neighbouring lots and roads in a 1-cell strip around the lot. It never samples the terrain. With no neighbour an
  edge stays at 0, which is pad height, so **the whole lot rectangle is levelled flat**. The pad shape only shows
  where an edge has a different height, i.e. next to a road or another lot.
- **`m_Smooth`** becomes `m_MinLimit`/`m_MaxLimit` (lower/raise regions; `CalculateLotInfo`, and `TerrainSystem`
  L967–972).
  - `m_MinLimit` is widened to cover the pad's flat edges.
  - `DontLower`/`DontRaise` and `Elevation > 0` make the corresponding limit empty, as `(w, w, -w, -w)` with
    `w` = smoothing width.
  - From that empty value it looks like the shader fades the limit rectangle out over `w = max(8, size/12)`.
    This is inferred, not verified.
- **Polygon terrain areas.** A `LotPrefab` with a `TerrainArea` component gets its triangles, node heights
  included, drawn into the heightmap (`TerrainSystem.CullAreasJob`). Fields: `m_HeightOffset` (20),
  `m_SlopeWidth` (20), `m_NoiseScale`, `m_NoiseFactor`, `m_AbsoluteHeight`. It is meant for mine piles. Never
  tried.
- **FlexIt** only deforms meshes. Its README says collision and lot shape stay rectangular.

## What was tried

1. **0.11.1: deformed flat pad.**
   - `FlatPadPlanner` maps the base prefab's pad octagon through the cage at ground level. It takes the convex
     hull, clips it to the lot minus a margin, and refits an octagon from the extreme points.
   - The fit is exact for quads whose edges face different quadrants, and inscribed otherwise.
   - Written for flat and evenly lifted variants; tilted lifts kept the point collapse.
   - **In game:** the log confirmed the octagon was written, but the ground looked unchanged. The reason is the
     edge-height finding above: with no neighbours, the whole rectangle is flat at pad height anyway.
2. **0.11.2: moving the `m_Smooth` sides.** Each side was shifted by how far the deformed footprint's bounding box
   moved (inward or outward). **In game:** the ground deformation still didn't work satisfactorily. This wasn't
   analysed further.
3. **Earlier, on master:** stepped `AdditionalBuildingTerraformElement` cells had no visible effect.

## Options not tried

- **`TerrainArea` sub-area.** Register a cloned `LotPrefab` with `TerrainArea` (noise 0, offset 0) and add it as a
  4-node sub-area at the deformed ground corners. Unknowns:
  - whether the surface is clean, and how it blends with the lot
  - save/load: area entities store a `PrefabRef` to the custom prefab, which needs preload registration
  - zone and overlap side effects (`PhysicalGeometry`)
- **Edge heights from the original terrain.** Write `Lot` edge heights from `BaseTerrainSystem` samples where the
  deformed lot is inset, so the gap ramps down to the natural ground. This needs base terrain for every deformed
  building at load, not just the edited one.
- **Harmony on `TerrainSystem.DrawHeightAdjustments`** (managed; edit `BuildingLotDraw` before upload, e.g. shear
  the axes). This is fragile: the shader behaviour is unknown, the CPU height lookups would disagree, and draws
  would have to be matched to buildings by position.
