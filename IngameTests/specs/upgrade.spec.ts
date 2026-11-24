import { describe, expect, it } from "vitest";
import { bridge } from "../src/bridge.js";
import { corner, offsets } from "../src/expect.js";
import { building } from "../src/fixture.js";
import { LogWatch } from "../src/log.js";
import { move, target, useCity } from "../src/setup.js";

/** Lets the simulation run for about `seconds` of real time at full speed, then pauses it again. */
async function simulate(seconds: number) {
  await bridge.speed(3);
  await bridge.frames(seconds * 30);
  await bridge.speed(0);
}

/** Every net node owned by the building still has an edge (nothing came apart). */
async function expectConnected(ref: string) {
  const nets = await bridge.nets(ref);
  for (const node of nets.nodes) expect(node.edges, `node ${node.id} at ${node.local}`).toBeGreaterThan(0);
}

describe("UPGRADE – upgrades and extensions", () => {
  useCity();

  it("UPGRADE-001 a car park's storey bends with it and its nets stay connected", async (context) => {
    const carPark = await building(context, "carPark");
    expect((await bridge.info(carPark)).upgrades.length).toBeGreaterThan(0);
    await target(carPark);
    const log = LogWatch.fromNow();
    await move("Corner", corner(1, 1, 1), [2, 0, 1]);
    // Logged once per building and session, so look at the whole log.
    const entity = (await bridge.building(carPark)).id.replace(/^#(\d+):(\d+)$/, "Entity($1:$2)");
    expect(LogWatch.fromStart().matching(/Upgrades of building/).some((line) => line.includes(entity))).toBe(true);
    await log.waitFor(/Lot sync for building .*, \d+ extensions/, 20_000);
    await expectConnected(carPark);
  });

  it("UPGRADE-002 resetting and undoing a deformed car park after some simulation doesn't crash", async (context) => {
    const carPark = await building(context, "carPark");
    await target(carPark);
    await move("Corner", corner(1, 1, 1), [2, 0, 1]);
    await simulate(20);
    await bridge.reset();
    expect((await bridge.settled()).deformed).toBe(false);
    await bridge.undo();
    expect((await bridge.settled()).deformed).toBe(true);
    await simulate(5);
    expect((await bridge.ping()).ok).toBe(true);
  });

  it("UPGRADE-004 a school with its wing survives simulation and reset; the wing stays plain", async (context) => {
    const school = await building(context, "school");
    const wing = (await bridge.info(school)).upgrades[0];
    await target(school);
    await move("Corner", corner(1, 1, 1), [1.5, 0, 0]);
    await simulate(20);
    await bridge.reset();
    expect((await bridge.settled()).deformed).toBe(false);
    if (wing) expect((await bridge.info(wing.id)).prefab).not.toMatch(/__uq/);
    await expectConnected(school);
  });

  it("UPGRADE-007 every 0.1 m nudge of a car park's top corner moves it; undo and redo restore each step", async (context) => {
    await target(await building(context, "carPark"), "Corner", corner(1, 1, 1));
    const heights: number[] = [];
    for (let i = 0; i < 3; i++) {
      await bridge.nudge([0, 1, 0]);
      heights.push(offsets((await bridge.settled()).targetOffsets)[7][1]);
    }
    expect(heights.map((h) => Math.round(h * 10))).toEqual([1, 2, 3]);
    for (let i = 2; i >= 0; i--) {
      await bridge.undo();
      expect(offsets((await bridge.settled()).targetOffsets)[7][1]).toBeCloseTo(i * 0.1, 5);
    }
    await bridge.redo();
    expect(offsets((await bridge.settled()).targetOffsets)[7][1]).toBeCloseTo(0.1, 5);
  });
});
