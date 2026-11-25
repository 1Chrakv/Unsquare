# 001 – Game crashes natively after a deformed building is removed

| | |
|---|---|
| Status | Open, not confirmed as an Unsquare bug |
| Found by | In-game test UNDO-004, run 3 (2026-11-24, save `UnsquareTest-Spike`) |
| Severity | High if confirmed: players lose unsaved progress |
| Requirement | UNDO-004 (old checklist row 52) |

## Summary

About one frame after a deformed building was removed, the game died with a native crash (no managed stack trace).
The building was removed by a **test-only shortcut** that added `Deleted` to the building entity directly, without
the bulldoze tool's clean-up. That shortcut alone may be the cause; it may also have exposed a real problem with
deleting buildings whose lot paths and areas Unsquare has moved.

## Steps that led to the crash

1. Load `UnsquareTest-Spike`, simulation paused, Unsquare mode on.
2. Select a `FireHouse02` (fire station), select the top face, nudge it up 0.1 m, wait until baked.
   Bake: `FireHouse02__uq8da043ee15b4`; lot sync moved 7 areas and 6 net edges, 4 road ends slid.
3. Step back twice (no target).
4. Add `Deleted` to the building entity (bridge `POST buildings/{ref}/bulldoze`, since removed).
5. Next frame: native crash.

## Expected

The building disappears like with the bulldozer; Ctrl+Z skips its edits and undoes the next older edit.

## Actual

```
Native Crash Reporting
Got a UNKNOWN while executing native code. This usually indicates
a fatal error in the mono runtime or one of the native libraries
used by your application.
Managed Stacktrace:
(empty)
```

Last mod log lines: the bake and lot sync above, `[Auto] >>> 822 POST …/bulldoze` → 200,
`[Auto] >>> 823 POST /v1/wait/frames` (never answered). No exception logged. No crash dump was written.

## Analysis

- The bulldoze tool creates a `CreationDefinition` with the delete flag, and on apply the game deletes the owned
  sub-objects, sub-nets and sub-areas and updates the connected roads. A raw `Deleted` skips all of that, which can
  leave owned nets and areas whose owner is gone: a plausible native crash in a Burst job.
- Unsquare-specific suspects if the mod is involved: lot nets remapped by `LotInstanceSync` (path ends slid along
  roads, `ConnectedNode` buffers updated), the variant's instance archetype (`VariantArchetype`), and the
  `SavedCage`/variant prefab references after the owner is gone.
- The case was not necessarily checked by hand regularly, so a real bug cannot be ruled out.

## Next steps

1. Same raw delete on (a) an undeformed building, (b) a deformed one, (c) a deformed-then-reset one, each after a
   fresh start. Crash only in (b) → Unsquare is involved.
2. Remove a deformed building with the game's real bulldozer (by hand, or the phase 3 input commands) — the case
   players hit.
3. If (b) or 2 crash: repeat with lot sync off (`AttachmentOptions` nets/areas) to narrow it to nets, areas or props.
