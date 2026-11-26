import fs from "node:fs";
/** The parts of a Vitest test context the helpers use (test functions receive it as their argument). */
export type TestContext = { task: { meta: object }; skip: () => void };
import { bridge, type BuildingJson } from "./bridge.js";
import { game } from "./game.js";
import { fixtureFile } from "./paths.js";

/**
 * The fixture city's named buildings (see Fixture.md). `fallback` lets a test run on any city when the fixture
 * lacks the entry: the first match of the query is used.
 */
export const fixtureBuildings = {
  plainHouse: { what: "A plain low-density house, no Placemaker row", fallback: { prefab: "ResidentialLow", variant: false } },
  sameTypeA: { what: "Two houses of the same prefab (A)", fallback: undefined },
  sameTypeB: { what: "Two houses of the same prefab (B)", fallback: undefined },
  gardenHouse: { what: "House with garden: fence, shed, trees, paving", fallback: undefined },
  storefront: { what: "Commercial building with a storefront sign and roof props", fallback: undefined },
  turnedNeighbour: { what: "House whose left neighbour lot is turned by 10–30°", fallback: undefined },
  raisedNeighbour: { what: "House whose left neighbour lot stands 2+ m higher or lower", fallback: undefined },
  slopeHouse: { what: "House on terrain falling away from its road by 3+ m", fallback: undefined },
  risingSlopeHouse: { what: "House cut into terrain rising behind it by 3+ m", fallback: undefined },
  slopedRoad: { what: "House whose street (front) runs up a slope", fallback: undefined },
  backRoad: { what: "House with a road along its back side", fallback: undefined },
  footpathHouse: { what: "House beside a free-standing footpath (parallel to its left side)", fallback: undefined },
  pathThroughLot: { what: "House with a footpath drawn through its lot parallel to a side, a road beyond", fallback: undefined },
  park: { what: "Park with curved paths and an entrance from a road", fallback: { prefab: "Park", variant: false } },
  carPark: { what: "Multi-storey car park with the extra storey upgrade", fallback: { prefab: "Parking", upgrades: true, variant: false } },
  school: { what: "Elementary school with the classroom extension wing", fallback: { prefab: "ElementarySchool", upgrades: true, variant: false } },
  hospital: { what: "Hospital with a ward wing and a helipad", fallback: { prefab: "Hospital", upgrades: true, variant: false } },
  growableL1: { what: "Level-1 zoned growable house", fallback: undefined },
} as const;

export type FixtureName = keyof typeof fixtureBuildings;

interface FixtureFile {
  save: string;
  buildings: Partial<Record<FixtureName, string>>;
}

export function readFixture(): FixtureFile {
  const fallback: FixtureFile = { save: "UnsquareTest-Fixture", buildings: {} };
  const file = fs.existsSync(fixtureFile) ? (JSON.parse(fs.readFileSync(fixtureFile, "utf8")) as FixtureFile) : fallback;
  return { ...file, save: process.env.UNSQUARE_SAVE ?? file.save };
}

export function writeFixture(file: FixtureFile) {
  fs.writeFileSync(fixtureFile, JSON.stringify(file, null, 2) + "\n");
}

/**
 * Makes sure the game runs with the fixture city loaded. Loads it once per run; spec files leave the city as they
 * found it (see {@link cleanUp}). `fresh` reloads anyway, e.g. after a test saved and reloaded.
 */
export async function loadFixture(options: { fresh?: boolean } = {}) {
  await game.ensureRunning();
  const { save } = readFixture();
  const state = (await bridge.state()).result;
  if (options.fresh || !state.inGame || state.save !== save) await bridge.load(save);
  await bridge.speed(0);
  await bridge.mode(true);
}

/** Puts the city back: no target, nothing deformed, empty history, default modes. */
export async function cleanUp() {
  const session = await bridge.session();
  if (!session.mode) await bridge.mode(true);
  for (let i = 0; i < 4 && (await bridge.session()).target; i++) await bridge.stepBack();
  // Reset each deformed building on its own first: Reset all leaves props displaced (issue 005).
  for (const deformed of await bridge.find({ variant: true, limit: 50 })) {
    try {
      await bridge.select(deformed.ref);
      await bridge.reset();
      await bridge.settled();
    } catch {
      // not selectable on its own (e.g. an extension): Reset all below takes it
    }
    for (let i = 0; i < 4 && (await bridge.session()).target; i++) await bridge.stepBack();
  }
  await bridge.resetAll();
  await bridge.clearHistory();
  await bridge.editMode("Both");
  await bridge.pick("Edge");
  await bridge.gizmo("Move");
  await bridge.alignMode("Angle");
  await bridge.fitMode("BackOnly");
  // The step cycles 0.1 → 0.5 → 2; bring it back to the default 0.1 m.
  for (let i = 0; i < 3 && (await bridge.session()).step !== 0.1; i++) await bridge.cycleStep();
}

/**
 * The fixture building's reference; skips the test when the fixture lacks it and no fallback is found.
 * Pass the test context so the skip is reported per test.
 */
export async function building(context: TestContext, name: FixtureName): Promise<string> {
  const recorded = readFixture().buildings[name];
  if (recorded) return recorded;
  const fallback = fixtureBuildings[name].fallback;
  if (fallback) {
    const found = (await bridge.find({ ...fallback, limit: 20 })).find((b: BuildingJson) => b.deformable);
    if (found) return found.ref;
  }
  return skip(context, `fixture building '${name}' missing (${fixtureBuildings[name].what}); see IngameTests/Fixture.md`);
}

/**
 * Puts the city back, then skips: a test skipped after it changed the city gets no afterEach clean-up, which would
 * leave the next test with a selected building.
 */
export async function skipAfterChanges(context: TestContext, reason: string): Promise<never> {
  await cleanUp();
  return skip(context, reason);
}

/** Skips the running test and records why in its meta data, which the report prints. */
export function skip(context: TestContext, reason: string): never {
  (context.task.meta as Record<string, unknown>).skipReason = reason;
  context.skip();
  throw new Error("unreachable");
}
