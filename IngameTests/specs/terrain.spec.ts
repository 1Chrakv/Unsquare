import { describe, expect, it } from "vitest";
import { bridge } from "../src/bridge.js";
import { corner, Face } from "../src/expect.js";
import { building } from "../src/fixture.js";
import { LogWatch } from "../src/log.js";
import { bakes, move, refused, target, twoOfAKind, useCity } from "../src/setup.js";

/** The terrain follows a lift a few frames after the swap (lot heights, then the terrain pass). */
const terrainSettles = () => bridge.frames(30);

/** Heights along the lot's centre line from back (−Z) to front (+Z), 1 m inside the lot. */
async function centreLine(ref: string, samples = 7) {
  const cage = (await bridge.session()).cage!;
  const { min, max } = cage.lotBox;
  const x = (min[0] + max[0]) / 2;
  const points: [number, number][] = [];
  for (let i = 0; i < samples; i++) points.push([x, min[2] + 1 + ((max[2] - min[2] - 2) * i) / (samples - 1)]);
  return bridge.heights(points, ref);
}

/** Height 1 m inside a lot corner (x/z sign: 0 = min, 1 = max). */
async function nearCorner(ref: string, x: 0 | 1, z: 0 | 1) {
  const { min, max } = (await bridge.session()).cage!.lotBox;
  const point: [number, number] = [x ? max[0] - 1 : min[0] + 1, z ? max[2] - 1 : min[2] + 1];
  return (await bridge.heights([point], ref))[0];
}

describe("TERRAIN – terrain lift", () => {
  useCity();

  it("TERRAIN-001 raising the front bottom edge slopes the whole lot from back to front", async (context) => {
    const house = await building(context, "plainHouse");
    await target(house);
    const log = LogWatch.fromNow();
    await move("Corner", corner(0, 0, 1), [0, 10, 0]);
    await move("Corner", corner(1, 0, 1), [0, 10, 0]);
    await terrainSettles();
    const heights = await centreLine(house);
    for (let i = 1; i < heights.length; i++)
      expect(heights[i], `no ridge: heights ${heights.join(", ")}`).toBeGreaterThanOrEqual(heights[i - 1] - 0.3);
    expect(heights[0]).toBeLessThan(2.5);
    expect(heights.at(-1)).toBeGreaterThan(7.5);
    await log.waitFor(/Terrain slope for prefab/);
  });

  it("TERRAIN-002 raising one bottom corner slopes the ground up towards it only", async (context) => {
    const house = await building(context, "plainHouse");
    await target(house);
    await move("Corner", corner(1, 0, 1), [0, 4, 0]);
    await terrainSettles();
    expect(await nearCorner(house, 1, 1)).toBeGreaterThan(2.5);
    for (const [x, z] of [[0, 0], [1, 0], [0, 1]] as const)
      expect(Math.abs(await nearCorner(house, x, z)), `corner ${x},${z} stays at ground`).toBeLessThan(1);
  });

  it("TERRAIN-003 raising the whole bottom face raises the lot evenly", async (context) => {
    const house = await building(context, "plainHouse");
    await target(house);
    await move("Face", Face.bottom, [0, 3, 0]);
    await terrainSettles();
    for (const height of await centreLine(house, 5)) expect(height).toBeCloseTo(3, 0);
  });

  it("TERRAIN-004 lowering a bottom corner sinks the ground towards it", async (context) => {
    const house = await building(context, "plainHouse");
    await target(house);
    await move("Corner", corner(1, 0, 1), [0, -2, 0]);
    await terrainSettles();
    expect(await nearCorner(house, 1, 1)).toBeLessThan(-1.2);
  });

  it("TERRAIN-005 reset flattens a lifted lot", async (context) => {
    const house = await building(context, "plainHouse");
    await target(house);
    await move("Face", Face.bottom, [0, 3, 0]);
    await terrainSettles();
    await bridge.reset();
    await bridge.settled();
    await terrainSettles();
    for (const height of await centreLine(house, 5)) expect(Math.abs(height)).toBeLessThan(0.3);
  });

  it("TERRAIN-007 the terrain toggle flattens and lifts the lot again", async (context) => {
    const house = await building(context, "plainHouse");
    await target(house);
    await move("Face", Face.bottom, [0, 3, 0]);
    expect((await bridge.session()).cage!.terrainLift).toBe(true);

    const log = LogWatch.fromNow();
    await bridge.toggleTerrain();
    const flat = await bridge.settled();
    await terrainSettles();
    expect(flat.terrainLift).toBe(false);
    await log.waitFor(/\[Edit\] Terrain lift off/);
    expect(bakes(log).at(-1)?.newAssets).toBe(0);
    expect(Math.abs((await centreLine(house, 3))[1])).toBeLessThan(0.5);

    await bridge.toggleTerrain();
    expect((await bridge.settled()).terrainLift).toBe(true);
    await terrainSettles();
    expect((await centreLine(house, 3))[1]).toBeCloseTo(3, 0);
  });

  it("TERRAIN-008 terrain lift is per building and undoable", async (context) => {
    const pair = await twoOfAKind();
    if (!pair) return context.skip();
    const [a, b] = pair;
    for (const house of [a, b]) {
      await bridge.stepBack();
      await bridge.stepBack();
      await target(house.ref);
      await move("Face", Face.bottom, [0, 2, 0]);
    }
    await bridge.toggleTerrain(); // b flat
    await bridge.settled();
    await terrainSettles();
    expect((await bridge.settled(b.ref)).terrainLift).toBe(false);
    expect((await bridge.settled(a.ref)).terrainLift).toBe(true);
    await bridge.undo();
    expect((await bridge.settled(b.ref)).terrainLift).toBe(true);
    await bridge.redo();
    expect((await bridge.settled(b.ref)).terrainLift).toBe(false);
  });

  it("TERRAIN-009 an undeformed building has no terrain lift to toggle", async (context) => {
    await target(await building(context, "plainHouse"));
    const log = LogWatch.fromNow();
    expect(await refused(bridge.toggleTerrain())).toBe("Rejected");
    await log.waitFor(/\[Edit\] Terrain lift on -> Rejected/);
  });
});
