# 005 – Reset all leaves props where the deformed variant had them

| | |
|---|---|
| Status | Open, confirmed (reproduced three times) |
| Found by | In-game tests LOT-002 and BLEND-001, run 8 (2026-11-25, `UnsquareTest-Fixture`), then by hand |
| Severity | Medium: after Reset all, props stand in the wrong place (up to 1.1 m) until the building is updated again |
| Requirement | RESET-001 (reset all), new RESET-004 (layout after reset all) |

## Summary

After **Reset all** (Alt+Shift+Delete, tool window), every building is back on its base prefab and reports an
undeformed cage, but its props keep the positions of the deformed variant. Areas and paths are restored correctly.
A single **Reset** of the same building restores the props exactly.

## Steps

1. Load `UnsquareTest-Fixture`, Unsquare mode on, select `gardenHouse` (`EE_ResidentialLow_L1_4x6@-829.6,340.0`).
2. Select corner 5 (left front bottom), drag it 1.3 m along −X, wait until baked.
3. Step back twice, Reset all.
4. Compare the building's props (`GET buildings/{ref}/subobjects`) with their positions before step 2.

## Expected

Every prop is back at its original position (0.00 m), as after a single Reset.

## Actual

85 of 98 props stay displaced, worst 1.10 m (flower bushes, street lights, garden lights, spawn and access
locations). Areas and net nodes: 0.00 m. `[Edit] Reset all: 1 buildings`, cage `deformed: false`.

| Sequence | Props worst | Areas | Nets |
|---|---|---|---|
| deform, Reset | 0.00 m | 0.00 | 0.00 |
| deform, Reset all | 1.10 m | 0.00 | 0.00 |
| deform with areas off (PROPS-005), Reset all | 1.10 m | 0.00 | 0.00 |

## Analysis

- `VariantSystem.ResetAll` (`IVariantPipeline.ResetAll`) calls `PrefabSwapper.Swap` for each building right away,
  from the session command, i.e. in the UI update phase. A single Reset queues the request and swaps in
  `VariantSystem.OnUpdate` (`TrySwap`).
- Props are rebuilt by the game from the prefab when the building is `Updated`. Swapping after this frame's
  modification phase likely lets the `Updated` tag be cleaned up before the sub-object systems see it, so props are
  never rebuilt. Areas and nets are rewritten by `LotInstanceSync` itself, which is why they come back.
- The test suite's clean-up used Reset all between tests, so later tests started from displaced props (LOT-002's
  "before" snapshot was the displaced layout; BLEND-001 likely the same).

## Possible fix

Let `ResetAll` request identity swaps for every deformed building and perform them in `VariantSystem.OnUpdate` like
a single reset (or mark the buildings so the swap runs there), instead of swapping from the UI phase.
