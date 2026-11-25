# Fixture city `UnsquareTest-Fixture`

The in-game tests run on one small city that holds a known set of buildings. Build it once, record which building
is which, and save it as **`UnsquareTest-Fixture`**. Tests load it, change buildings, and put everything back after each
test; they never save over it (they only save as `UnsquareTest-Run…`).

## Rules

- **No deformed buildings.** Tests reset every deformation in the city after each test (`resetAll`).
- **Placemaker and FlexIt off** while you build (their tests use their own saves later), so row houses stay plain.
- Unlimited money and all milestones (map options) make placing service buildings easy.
- Keep it small: any flat-ish map, one corner of it. Loading time grows with the city.
- Simulation paused when you save; the tests keep it paused unless they need days to pass.

## Buildings to place

Plop growables with a mod like Plop the Growables or Find It, or zone and wait; then remove the mod's
influence again if possible. Leave at least one empty lot width between unrelated groups unless the entry asks
for a neighbour.

| Name | What to build | Used by |
|---|---|---|
| `plainHouse` | A detached low-density house (not a row house) on flat ground at a straight road | Most edit tests |
| `sameTypeA`, `sameTypeB` | Two houses of the **same prefab** (same name), each on its own flat lot | BAKE-001, TERRAIN-008 |
| `gardenHouse` | A low-density house whose lot has a garden: fence, shed, trees, paving | PROPS-004/005, LOT-002, SPLIT-003/004 |
| `storefront` | A low-density commercial building with a storefront sign and props on its roof | PROPS-001 to 003 |
| `turnedNeighbour` | A house whose **left** neighbour (seen from the street) is turned by 10–30°: put the neighbour on a slightly curved road or a corner | ALIGN-001 to 003, ALIGN-006/007 |
| `raisedNeighbour` | A house whose left neighbour stands 2 m or more higher (terrain step or slope between them) | LEVEL-001, LEVEL-003 |
| `slopeHouse` | A house on terrain that falls away from its road by 3 m or more towards the back | FIT-001 to 005, TERRAIN-001 to 005 |
| `slopedRoad` | A house at a road that climbs along the house's front (one front corner higher than the other) | LEVEL-004, FIT-001 |
| `backRoad` | A house with a second road running along its back side (a few metres away) | ALIGN-004, LEVEL-002 |
| `footpathHouse` | A house with a free-standing footpath (built with the path tool, not part of a lot) parallel to its left side, 2–5 m away | ALIGN-009, LEVEL-005 |
| `pathThroughLot` | A house with a footpath drawn across its lot, parallel to its left side, and a road a few metres beyond the lot on that side | ALIGN-010 |
| `park` | A park with curved paths and an entrance from a road (e.g. a medium park) | LOT-003 to 005, SEL-007 |
| `carPark` | A multi-storey car park with the extra storey upgrade installed | UPGRADE-001 to 008, SPLIT-011/015 |
| `school` | An elementary school with the classroom extension wing installed | UPGRADE-004/005, SEL-008 |
| `hospital` | A hospital with a ward wing and the helipad installed | SPLIT-014/015 |
| `growableL1` | A zoned level-1 house with residents (for level-up tests) | LEVELUP-001 to 003 |

## Recording the buildings

With the city loaded, a Debug build deployed and the bridge enabled (`npm run bridge:enable` before starting
the game):

1. Turn on Unsquare mode and click a building (no handle needed).
2. Run `npm run fixture:record <name>` in `IngameTests/`, e.g. `npm run fixture:record gardenHouse`.
3. Repeat for every name (`npm run fixture:record -- --list` lists them), then save the city as `UnsquareTest-Fixture`.
4. `npm run fixture:check` loads the save and reports anything missing or no longer deformable.

The names and positions go to `IngameTests/fixture.json`, which is committed. Tests find each building by prefab
and position, so moving or replacing one means recording it again.
