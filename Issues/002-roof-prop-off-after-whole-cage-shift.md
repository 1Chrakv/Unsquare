# 002 – Roof prop lands 0.36 m off after shifting the whole cage

| | |
|---|---|
| Status | Open, suspected bug; reproduced with the same 0.36 m in run 5 |
| Found by | In-game test DRAG-010, run 3 (2026-11-24, save `UnsquareTest-Spike`) |
| Severity | Low to medium: visible offset of roof props on moved buildings |
| Requirement | DRAG-010 (old checklist row 154) |

## Summary

Dragging the whole cage 3 m sideways (all eight corners offset by `[3, 0, 0]`, a pure translation) should move
every prop by exactly 3 m. A roof prop, `SatelliteDishRoof03`, ended up 0.36 m away from that position in plan
(x/z only; height not compared). The other props were within 0.3 m.

## Steps

1. Load `UnsquareTest-Spike`, Unsquare mode on, pick a plain low-density house (the suite's `plainHouse` fallback query).
2. Pick mode Cage, select the whole cage.
3. Drag it `[3, 0, 0]` over 10 frames and release; wait until baked.
4. Compare each prop's building-local position before and after (`GET buildings/{ref}/subobjects`).

## Expected

Each prop moved by `[3, 0, 0]` in building space (tolerance 0.3 m; with a pure translation it should be exact).

## Actual

`prop SatelliteDishRoof03: expected 0.36 to be less than 0.3` — the nearest dish of that prefab to the expected
position is 0.36 m away from it in plan.

## Analysis

- With a pure translation the cage's Jacobian is the identity, so `RigidDeformer.Place` should only shift the
  anchor. A plan offset suggests something else moves attached props: the attached-prop mode
  (`AttachmentOptions.AttachedPropMode`), facade alignment (`FacadeReachMeters`, evaluated on the mesh box vs the lot
  box), or a snap to the deformed roof.
- Or the test matched the wrong prop: if the house has several dishes of that prefab, or the prop is a
  random/placeholder prop re-rolled when the building is rebuilt, the nearest one may not be the same prop.

## Next steps

1. List all `SatelliteDishRoof03` instances before and after with their `forward`/`up` vectors; check whether the
   count and the rest of the set match exactly.
2. Repeat with facade alignment off (`FacadeReachMeters = 0`) and with each attached-prop mode.
3. If it is the matching, make the test pair props by sub-object index instead of nearest position.
