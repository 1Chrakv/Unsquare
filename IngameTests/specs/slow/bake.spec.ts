import { beforeAll, describe, expect, it } from "vitest";
import { bridge } from "../../src/bridge.js";
import { corner } from "../../src/expect.js";
import { building, cleanUp, loadFixture } from "../../src/fixture.js";
import { LogWatch } from "../../src/log.js";
import { bakes, move, target } from "../../src/setup.js";

describe("BAKE – memory and zoning over many buildings", () => {
  beforeAll(async () => {
    await loadFixture({ fresh: true });
    await cleanUp();
  });

  it("BAKE-004 bakes of 15 large buildings hold at most about 1,000,000 source vertices", async () => {
    const all = (await bridge.find({ variant: false, limit: 2000 })).filter((b) => b.deformable && b.lotSize);
    const large = all.sort((a, b) => b.lotSize![0] * b.lotSize![1] - a.lotSize![0] * a.lotSize![1]).slice(0, 15);
    expect(large.length).toBe(15);
    const log = LogWatch.fromNow();
    for (const b of large) {
      await target(b.ref);
      await move("Corner", corner(1, 1, 1), [1, 0, 0]);
      for (let i = 0; i < 4 && (await bridge.session()).target; i++) await bridge.stepBack();
    }
    const held = bakes(log).map((bake) => Number(/\((\d+) vertices\)/.exec(bake.line)?.[1] ?? NaN));
    expect(held.length, "one bake per building").toBeGreaterThanOrEqual(15);
    // The budget evicts after each bake: what stays held between bakes is within it (a margin for the last one).
    expect(Math.max(...held), `sources held: ${held.join(", ")}`).toBeLessThan(1_100_000);
    await cleanUp();
  }, 1_200_000);

  it("BAKE-008 zoning a large area with the base building's zone never spawns a deformed variant", async (context) => {
    const growable = await building(context, "growableL1");
    const zone = Object.keys((await bridge.zone(growable)).inside)[0];
    await target(growable);
    await move("Corner", corner(1, 1, 1), [1, 0, 0]);
    await bridge.stepBack();
    await bridge.stepBack();
    const { position } = await bridge.building(growable);
    const points: [number, number][] = [[position[0], position[2]]];
    const painted = (await bridge.paintZone({ zone, points, radius: 200, onlyEmpty: true })).result.cells;
    const before = new Set((await bridge.find({ limit: 5000 })).map((b) => b.ref));
    await bridge.simulate({ days: 0.25 });
    const spawned = (await bridge.find({ limit: 5000 })).filter((b) => !before.has(b.ref));
    const variants = (await bridge.find({ variant: true, limit: 50 })).map((b) => b.ref);
    expect(variants, `${painted} cells zoned ${zone}, ${spawned.length} buildings spawned`).toEqual([growable]);
    for (const b of spawned) expect(b.prefab, b.ref).not.toMatch(/__uq/);
  }, 1_800_000);
});
