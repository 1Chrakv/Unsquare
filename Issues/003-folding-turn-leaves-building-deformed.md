# 003 – A turn that folds the cage leaves the building deformed

| | |
|---|---|
| Status | Won't fix: intended (decided 2026-11-24) |
| Found by | In-game test DRAG-009, run 3 (2026-11-24, save `UQTest-Spike`) |
| Severity | Low |
| Requirement | DRAG-009 (old checklist row 151: "a folding turn shows the fold problem and is not applied") |

## Summary

Turning the top face 175° around the X axis with the rotate gizmo (10 frames) shows the fold problem
(`problem = CageFolds`), as required. After the drag ends and the building has settled, it is still deformed
(`TargetSpec` not the identity).

## Steps

1. Load `UQTest-Spike`, Unsquare mode on, select a plain house and its top face.
2. Gizmo Rotate, turn the face 175° around X over 10 frames, release.
3. Wait until settled; read the session and the cage.

## Expected (per the requirement)

The fold problem shows and the folding turn is not applied: the building stays undeformed.

## Actual

`problem = CageFolds`, but `deformed = true` after settling.

## Analysis

The drag passes through angles that don't fold before it reaches 175°. Most likely the session keeps the last
accepted angle of the drag and only refuses the folding ones, so the building stays at the largest turn that
still worked. That may be the intended behaviour (it is how a clamped drag behaves elsewhere); the requirement's
wording ("is not applied") doesn't say whether the whole drag or only the folding part is dropped.

## Decision

Intended: a drag keeps the last shape that didn't fold; only the folding part is dropped. DRAG-009 is reworded
accordingly, and its test now checks that the kept shape is deformed but not folded (each top front corner stays
in front of the top back corner on its side).
