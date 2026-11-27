import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";
import { bridge } from "../src/bridge.js";
import { expectApplied, Face } from "../src/expect.js";
import { building } from "../src/fixture.js";
import { LogWatch } from "../src/log.js";
import { modData } from "../src/paths.js";
import { bakes, target, twoOfAKind, useCity } from "../src/setup.js";

/** A shape no other test uses, so its variant is new to this run's first bake. */
const deform = async () => {
  await bridge.handle("Corner", 6);
  await bridge.nudge([0, 1, 0]);
  await bridge.handle("Corner", 7);
  await bridge.nudge([1, 0, 0]);
  return bridge.settled();
};

describe("BAKE – variants and baking", () => {
  useCity();

  it("BAKE-001 the same offsets on a second building of the type reuse the variant", async (context) => {
    const pair = await twoOfAKind();
    if (!pair) return context.skip();
    await target(pair[0].ref);
    expectApplied(await deform());
    await bridge.stepBack();
    await bridge.stepBack();

    const log = LogWatch.fromNow();
    await target(pair[1].ref);
    expectApplied(await deform());
    const second = bakes(log).at(-1);
    expect(second?.newAssets, second?.line).toBe(0);
  });

  it("BAKE-002 baked variants have bounds and geometry assets in ModsData", async (context) => {
    await target(await building(context, "plainHouse"));
    const log = LogWatch.fromNow();
    expectApplied(await deform());
    const bake = bakes(log).at(-1);
    expect(bake?.variant, bake?.line).toBeTruthy();
    expect(fs.existsSync(path.join(modData, "bounds.json"))).toBe(true);
    const assets = fs.readdirSync(modData, { recursive: true }).map(String).filter((name) => /UQ_.*\.Geometry$/.test(name));
    expect(assets.length).toBeGreaterThan(0);
  });

  it("BAKE-005 repeated nudges of one building keep its bake time", async (context) => {
    await target(await building(context, "plainHouse"), "Face", Face.top);
    const log = LogWatch.fromNow();
    for (let i = 0; i < 6; i++) {
      await bridge.nudge([0, 1, 0]);
      await bridge.settled();
    }
    const times = bakes(log).map((bake) => bake.ms);
    expect(times.length).toBeGreaterThanOrEqual(5);
    // The first bake may load the sources; later ones reuse them.
    const later = times.slice(1).sort((a, b) => a - b);
    const median = later[Math.floor(later.length / 2)];
    expect(median, `bake times ${times.join(", ")} ms`).toBeLessThan(250);
  });
});
