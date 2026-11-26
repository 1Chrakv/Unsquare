import { afterAll, beforeAll, describe, expect, it } from "vitest";
import { bridge, type CageJson } from "../../src/bridge.js";
import { corner, distanceXZ, Face } from "../../src/expect.js";
import { building, cleanUp, loadFixture, skip } from "../../src/fixture.js";
import { LogWatch } from "../../src/log.js";
import { expectConnected, move, target } from "../../src/setup.js";

const RunSave = "UnsquareTest-Run-LevelUp";
const Timeout = 2_400_000;
const Bottom = [0, 1, 4, 5];

/** Starts the game's level-up of the building and lets the city run until the new level stands. */
async function levelUp(ref: string) {
  const level = (await bridge.info(ref)).level!;
  await bridge.levelUp(ref);
  for (let i = 0; i < 60; i++) {
    const info = await bridge.info(ref);
    if (!info.underConstruction && info.level === level + 1) return info;
    await bridge.simulate({ frames: 2048 });
  }
  throw new Error(`The building did not reach level ${level + 1}.`);
}

/** A lot-corner move and a lean, so the carried shape has a lot outline and a lean to scale. */
async function deform(ref: string) {
  await target(ref);
  await move("Corner", corner(1, 0, 1), [1.5, 0, 0]);
  await move("Face", Face.top, [1, 0, 0]);
  const cage = await bridge.settled();
  await bridge.stepBack();
  await bridge.stepBack();
  return cage;
}

const expectSameOutline = (after: CageJson, before: CageJson) =>
  Bottom.forEach((c) => expect(distanceXZ(after.corners[c], before.corners[c]), `lot corner ${c}`).toBeLessThan(0.15));

describe("LEVELUP – deformed growables levelling up", () => {
  let keep: unknown;
  let carried: { ref: string; spec: string } | undefined;
  beforeAll(async () => {
    await loadFixture({ fresh: true });
    await cleanUp();
    keep = (await bridge.settings()).KeepOnLevelUp;
    await bridge.setSettings({ KeepOnLevelUp: true });
  });
  afterAll(async () => {
    await bridge.setSettings({ KeepOnLevelUp: keep });
  });

  it("LEVELUP-001 a deformed level-1 growable that levels up keeps its lot outline and lean", async (context) => {
    const growable = await building(context, "growableL1");
    const before = await deform(growable);
    const log = LogWatch.fromNow();
    const info = await levelUp(growable);
    await log.waitFor(/levelled up: carrying its cage over/, 60_000);
    expect(info.prefab, "on a variant of the new level").toMatch(/__uq/);
    const after = await bridge.settled(growable);
    expectSameOutline(after, before);
    const lean = (cage: CageJson) => cage.corners[corner(1, 1, 1)][0] - cage.corners[corner(1, 0, 1)][0];
    expect(Math.sign(lean(after)), "still leaning the same way").toBe(Math.sign(lean(before)));
    expect(Math.abs(lean(after)), "still leaning").toBeGreaterThan(0.2);
    await expectConnected(growable);
    carried = { ref: growable, spec: after.targetSpec };
  }, Timeout);

  it("LEVELUP-002 the carried shape survives a reload; with Keep shape on level-up off the next one is plain", async (context) => {
    if (!carried) skip(context, "needs the level-up of LEVELUP-001");
    await bridge.save(RunSave);
    await bridge.load(RunSave);
    await bridge.speed(0);
    await bridge.mode(true);
    expect((await bridge.settled(carried.ref)).targetSpec).toBe(carried.spec);
    await bridge.setSettings({ KeepOnLevelUp: false });
    const info = await levelUp(carried.ref);
    expect(info.prefab, "comes back undeformed").not.toMatch(/__uq/);
    expect((await bridge.settled(carried.ref)).deformed).toBe(false);
    await bridge.setSettings({ KeepOnLevelUp: true });
  }, Timeout);

  it("LEVELUP-003 a growable saved while levelling up keeps its shape when the level-up finishes after a load", async (context) => {
    await loadFixture({ fresh: true });
    const growable = await building(context, "growableL1");
    const before = await deform(growable);
    await bridge.levelUp(growable); // under construction now
    await bridge.save(RunSave);
    await bridge.load(RunSave);
    await bridge.speed(0);
    await bridge.mode(true);
    for (let i = 0; i < 60 && (await bridge.info(growable)).underConstruction; i++) await bridge.simulate({ frames: 2048 });
    const info = await bridge.info(growable);
    expect(info.underConstruction).toBe(false);
    expect(info.level).toBe(2);
    expect(info.prefab, "on a variant of the new level").toMatch(/__uq/);
    expectSameOutline(await bridge.settled(growable), before);
  }, Timeout);
});
