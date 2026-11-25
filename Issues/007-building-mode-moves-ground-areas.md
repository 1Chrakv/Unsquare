# 007 – In Building mode without blending, ground areas move with the building

| | |
|---|---|
| Status | Open, confirmed |
| Found by | In-game test BLEND-001, runs 7–9 (2026-11-25, `UnsquareTest-Fixture`), then by hand |
| Severity | Medium: garden areas shift up to 2.6 m although only the building was edited |
| Requirement | BLEND-001 (old checklist row 140), SPLIT-003/004 |

## Summary

With edit mode **Building** and blending off (`BlendMode None`, the default), moving the building's left wall leaves
the lot cage untouched, as intended, but the lot's ground areas (grass, sand, hang-around areas) still move. By design
ground items follow the lot cage unless blending is on.

## Steps

1. Load `UnsquareTest-Fixture`, Unsquare mode on, select `gardenHouse` (`EE_ResidentialLow_L1_4x6@-829.6,340.0`).
2. Edit mode Building; select corner 5 (left front bottom), drag `[-1.5, 0, 0]`; select corner 7 (left front top),
   drag `[-1.5, 0, 0]`; wait until baked, then 30 frames.
3. Compare `GET buildings/{ref}/areas` with before step 2.

## Actual

Cage after step 2: `lot` all zero (`lotDeformed false`), `blend None`, building cage
`-16,0,0;73,0,0;-16,0,0;73,0,0;6,0,0;-27,0,0;6,0,0;-27,0,0` (lot-box frame, 0.1 m units).

| Area | Point | Moved in plan |
|---|---|---|
| Grass Surface 01 (area 10) | 3 | 2.57 m |
| Grass Surface 01 (area 4) | 1 | 2.37 m |
| Grass Surface 01 (area 8) | 3 | 2.21 m |
| Grass Surface 01 (area 5) | 0 | 1.07 m |
| Grass Surface 01 (area 11) | 0 | 0.77 m |
| Hangaround Area (area 2) | 0 | 0.45 m |
| Sand Surface 02 (area 6) | 15 | 0.36 m |
| Hangaround Area (area 1) | 3 | 0.08 m |

Several of these points lie in the back garden, outside the building's footprint.

## Analysis

- Ground items should be deformed with the ground deformation (`SplitDeformations` / `AttachmentContext`: the lot
  cage, or the blend while blending is on). Either the area deformer uses the building cage for split specs, or
  `LotInstanceSync` matches the instance areas to the new prefab layout wrongly.
- The garden house's variant logs `areas 11/12` for some split variants, so one area was left out in those bakes;
  compare with `areas 12/12` for linked ones.
- SPLIT-003/004 (ground stays in Building mode) are review requirements and not yet automated, which is why only
  BLEND-001 caught it.
