# 006 – Utility connection nodes stay at the building's original place

| | |
|---|---|
| Status | Open, confirmed; impact unknown (the nodes are invisible) |
| Found by | In-game test DRAG-010, run 9 (2026-11-25, `UnsquareTest-Fixture`), then by hand |
| Severity | Low to medium: possibly none visible, but the building's water, sewage and power hook-ups stay put |
| Requirement | DRAG-010 (old checklist row 154) |

## Summary

After dragging the whole cage of `plainHouse` (`CN_HighOffice01_L1_4x4@-837.1,539.5`) 3 m along +X, every prop
moves 3 m, except the three utility connection markers, which stay at the building's original centre:
`Sewage Pipe Node 1.5`, `Water Pipe Node 1`, `Ground Cable Node 1`.

## Steps

1. Load `UnsquareTest-Fixture`, Unsquare mode on, select `plainHouse`, pick mode Cage, select the whole cage.
2. Drag it `[3, 0, 0]` over 3 frames and release; wait until baked, then 120 more frames.
3. Read `GET buildings/{ref}/subobjects`.

## Actual

| Prop | Before | After |
|---|---|---|
| Pedestrian Spawn Location | 0.0, 0.2, 9.6 | 3.0, 0.2, 9.6 |
| RooftopAccess05 | 9.7, 25.4, −6.8 | 12.7, 25.4, −6.8 |
| Sewage Pipe Node 1.5 | −1.2, −1.8, 0.0 | **−1.2, −1.8, 0.0** |
| Water Pipe Node 1 | 1.2, −1.1, 0.0 | **1.2, −1.1, 0.0** |
| Ground Cable Node 1 | 0.0, −0.9, 0.0 | **0.0, −0.9, 0.0** |

The bake logs `props … 37/37` for the variant, so the prefab's sub-objects are all deformed.

## Analysis

- The nodes are not rebuilt from the variant prefab's `ObjectSubObjects` (or they are, but something places them
  afterwards). Likely the game creates or positions utility nodes separately, e.g. as saved entities tied to the
  underground networks, like areas and nets, which `LotInstanceSync` has to move itself.
- To check: whether the nodes are owned sub-objects that keep their `Transform` on `Updated`, and what the game does
  with a building's utility connection when the building is moved by Move It (as a reference for expected behaviour).
- Check in game whether a moved building still connects to water, sewage and power in the simulation.
