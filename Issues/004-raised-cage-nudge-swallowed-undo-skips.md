# 004 – On a raised cage, a 0.1 m nudge can do nothing and undo skips a step

| | |
|---|---|
| Status | Open, confirmed |
| Found by | In-game test UPGRADE-007, run 5 (2026-11-24, save `UnsquareTest-Spike`) |
| Severity | Medium: a visible nudge without effect, and Ctrl+Z jumping two steps |
| Requirement | UPGRADE-007 (old checklist row 114) |

## Summary

A car park with its extra storey (`ParkingHall03`) is edited on a cage raised to the storey's top (`EditBox`), but
its variants are named and baked on the lower lot box. Three 0.1 m nudges up of the top corner 7 (edit mode Both):

| Step | Edit offsets shown | Bake | Undo history (lot-frame specs) |
|---|---|---|---|
| Nudge 1 | 0.1 | `ParkingHall03__uq5675befb11c1` | 0 → A |
| Nudge 2 | 0.2 | **none**: the lot-frame spec didn't change | nothing new, or A → A |
| Nudge 3 | 0.3 | `ParkingHall03__uq92432a292e85` | A → B |
| Undo | **0.1** (expected 0.2) | `…uq5675befb11c1` again | back to A |

## Steps

1. Load a city with a multi-storey car park that has the extra storey installed; Unsquare mode on, edit mode Both.
2. Select the car park, pick mode Corner, select corner 7 (top, front, left as seen from the street).
3. Nudge up three times (0.1 m step), waiting for each bake.
4. Ctrl+Z once.

## Expected

Each nudge moves the storey's roof by one step; each undo takes back exactly one nudge (0.3 → 0.2).

## Actual

The second nudge moves the cage overlay but not the building, and the first undo lands on 0.1.

## Analysis

- `DeformService.SetOffsets` (Both) converts the raised edit offsets to the lot box
  (`ExtendedCage.Convert`, exact up to the 0.1 m grid). At the top of the raised box a 0.1 m step maps to less
  than 0.1 m on the lot box and can round to the same lot offsets, so the spec, and the building, stay the same.
- `DeformService.View` hides this for display: it prefers the cached edit offsets (`_editOffsets`) while they match the spec,
  so the overlay shows 0.2.
- The history (`EditHistory`) records lot-frame specs (`CageEditSession`, `_history.Record(Target, before, after)`).
  Undo restores lot spec A, for which the cached edit offsets (0.2) no longer match, so the view is converted back
  from the lot box and shows 0.1.

## Possible fixes (to decide)

- Store the edit offsets with each history entry (or the edit box along with the spec) and restore the view from
  them on undo/redo, so undo returns to exactly what was shown.
- Make the lot box conversion never swallow a nudge: round away from the previous lot offsets when the edit offsets
  changed, so every visible step changes the building.
- Both changes touch how specs are named only if rounding changes; check `VariantNaming` compatibility before
  (save-format rule in CLAUDE.md).
