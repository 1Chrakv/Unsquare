import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";
import { bridge } from "../src/bridge.js";
import { expectApplied, Face } from "../src/expect.js";
import { building } from "../src/fixture.js";
import { LogWatch } from "../src/log.js";
import { modData } from "../src/paths.js";
import { bakes, target, twoOfAKind, useCity } from "../src/setup.js";
import { pack } from "../src/review.js";
import { lookAt, shot } from "../src/view.js";

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

  it("BAKE-003 taper, skew and twist bake into smooth meshes (with a mesh review)", async (context) => {
    const house = await building(context, "plainHouse");
    const plain = (await bridge.meshes(house)).meshes;
    const shots: Record<string, string> = {};
    const shapes: Record<string, string> = {};
    const shapesToTry: [string, () => Promise<unknown>][] = [
      ["taper", async () => {
        for (const c of [2, 6]) {
          await bridge.handle("Corner", c);
          await bridge.drag({ translate: [3, 0, 0], frames: 3 });
        }
        for (const c of [3, 7]) {
          await bridge.handle("Corner", c);
          await bridge.drag({ translate: [-3, 0, 0], frames: 3 });
        }
      }],
      ["skew", async () => {
        await bridge.handle("Face", Face.top);
        await bridge.drag({ translate: [4, 0, 0], frames: 4 });
      }],
      ["twist", async () => {
        await bridge.handle("Face", Face.top);
        await bridge.drag({ rotate: { axis: 1, degrees: 25 }, frames: 6 });
      }],
    ];
    for (const [name, apply] of shapesToTry) {
      await target(house);
      await apply();
      const cage = await bridge.settled();
      expectApplied(cage);
      expect((await bridge.meshes(house)).meshes.length, `${name}: meshes`).toBe(plain.length);
      shapes[name] = cage.targetOffsets;
      await bridge.clearHandle();
      await lookAt(house, "front");
      shots[name] = await shot(`BAKE-003-${name}`, { crop: "target", ui: false });
      await lookAt(house, "left");
      shots[`${name}-side`] = await shot(`BAKE-003-${name}-side`, { crop: "target", ui: false });
      await bridge.reset();
      await bridge.settled();
      await bridge.stepBack();
    }
    await pack("BAKE-003", {
      pack: "mesh",
      shots,
      probe: { shapes, meshes: plain.length },
      focus: "The office tower tapered (narrower top), skewed (top shifted sideways) and twisted (top turned 25°), each from the front and the side. Facades must stay smooth and evenly lit.",
    });
  }, 300_000);

  it("BAKE-006 plain buildings render at every LOD after their sources were released (with a mesh review)", async (context) => {
    const plainRef = await building(context, "storefront");
    const log = LogWatch.fromNow();
    // Deform and reset a few buildings so the source mesh cache releases meshes.
    for (const name of ["plainHouse", "gardenHouse", "sameTypeA", "sameTypeB"] as const) {
      await target(await building(context, name), "Face", Face.top);
      await bridge.nudge([0, 1, 0]);
      await bridge.settled();
      await bridge.reset();
      await bridge.settled();
      await bridge.stepBack();
      await bridge.stepBack();
    }
    const shots: Record<string, string> = {};
    for (const distance of [60, 250, 700]) {
      await lookAt(plainRef, "front", distance);
      await bridge.frames(10);
      shots[`at-${distance}m`] = await shot(`BAKE-006-${distance}`, { ui: false, maxPx: 1600 });
    }
    expect(log.matching(/Exception|asset .*(missing|failed)|could not load/i), "no asset errors").toEqual([]);
    expect((await bridge.building(plainRef)).variant, "still the plain prefab").toBe(false);
    await pack("BAKE-006", {
      pack: "mesh",
      shots,
      building: plainRef,
      log,
      focus: "The storefront building in the middle at 60 m, 250 m and 700 m: never missing, pink, black or flickering.",
    });
  }, 300_000);
});
