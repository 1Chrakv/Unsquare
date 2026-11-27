# 011 – Screen-sized gizmo and pick radii come out about 16 % too small

| | |
|---|---|
| Status | Open, confirmed by measurement; cause suspected |
| Found by | In-game test GIZMO-005 (2026-11-27, `UnsquareTest-Fixture`, Debug build of `feature/ingame-input`) |
| Severity | Low: the gizmo is a bit smaller than set, pick radius and drag threshold a bit tighter |
| Requirement | GIZMO-005 (old row 47); also affects OPTIONS-005, OPTIONS-006 |

## Summary

With default settings the gizmo should be 6 % of the view height. Measured on screen it is 5.0–5.1 % at every zoom,
because the mod computes screen sizes from `Camera.main.fieldOfView` (31.7°), while the rendered projection has an
effective vertical field of view of about 37.3°.

## Steps

1. Reset settings (`GizmoScreenSize` 0.06), select `plainHouse`'s top face.
2. Camera front view at 80, 250 and 600 m (`view/camera`).
3. Project the gizmo origin and a point 10 m across the view (`view/screenPoint`, which uses
   `Camera.main.WorldToScreenPoint`): pixels per metre at the gizmo's distance give the field of view actually
   rendered; the gizmo length (`gizmo.length`) gives the one the mod assumed.

## Actual

| Zoom | Distance to gizmo | Gizmo length | Field of view rendered | Field of view assumed | Gizmo share of screen height |
|---|---|---|---|---|---|
| 80 | 74.1 m | 2.53 m | 36.9° | 31.7° | 5.1 % |
| 250 | 251.3 m | 8.58 m | 37.4° | 31.7° | 5.0 % |
| 600 | 619.7 m | 21.14 m | 37.3° | 31.7° | 5.1 % |

## Analysis

`CameraView.TryCapture` builds `ViewScale` from `camera.fieldOfView` and `Screen.height`. The game's camera seems to
render with a projection that doesn't match that property (Cinemachine lens or HDRP physical camera settings), so every
pixel-based size (`GizmoScreenSize`, `PickRadiusPixels`, `FarClickMarginPixels`, `DragThresholdPixels`) is about 0.85×
what the options say. Not yet checked: where the game takes its field of view from.

## Next steps

- Derive `ViewHeightPerMeter` from the projection matrix (`2 / camera.projectionMatrix.m11`) instead of
  `fieldOfView`, or read the game's lens settings; check against `view/screenPoint`.
- GIZMO-005 asserts 6 % ± 0.5 % and turns green with the fix.
