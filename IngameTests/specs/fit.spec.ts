import { describe, expect, it } from "vitest";
import { bridge, type CageJson } from "../src/bridge.js";
import { corner, offsets } from "../src/expect.js";
import { building } from "../src/fixture.js";
import { LogWatch } from "../src/log.js";
import { target, useCity } from "../src/setup.js";

const Back = [corner(0, 0, 0), corner(1, 0, 0)];
const Front = [corner(0, 0, 1), corner(1, 0, 1)];

/** Fit to ground runs a frame or two later (terrain read back from the GPU); waits for its bake. */
async function fit(mode: "BackOnly" | "AllCorners"): Promise<CageJson> {
  await bridge.fitMode(mode);
  const log = LogWatch.fromNow();
  await bridge.fit();
  await log.waitFor(/\[Edit\] Fit to ground \((back|all corners)\) on .* -> Accepted/, 20_000);
  return bridge.settled();
}

/** Heights along the lot's centre line from back to front (20–80 %), relative to the building's ground. */
async function centreLine(ref: string, samples = 5) {
  const { min, max } = (await bridge.session()).cage!.lotBox;
  const x = (min[0] + max[0]) / 2;
  const points = Array.from({ length: samples }, (_, i) => [x, min[2] + (max[2] - min[2]) * (0.2 + (0.6 * i) / (samples - 1))] as [number, number]);
  return bridge.heights(points, ref);
}

/** Column heights (top minus bottom) of the four corner columns. */
const columns = (cage: CageJson) => [0, 1, 4, 5].map((c) => cage.corners[c + 2][1] - cage.corners[c][1]);

describe("FIT – fit to ground", () => {
  useCity();

  it("FIT-001 back-only fit puts the back on the original slope and keeps the building's height", async (context) => {
    await target(await building(context, "slopeHouse"));
    const before = (await bridge.session()).cage!;
    const log = LogWatch.fromNow();
    const after = await fit("BackOnly");
    await log.waitFor(/\[Fit\] Building .*: original ground at the corners/);
    const moved = offsets(after.targetOffsets);
    expect(Back.some((c) => Math.abs(moved[c][1]) > 0.5), "the back follows the slope").toBe(true);
    for (const c of Front) expect(Math.abs(moved[c][1]), "the street side stays at road level").toBeLessThan(0.5);
    columns(after).forEach((h, i) => expect(h).toBeCloseTo(columns(before)[i], 1));
  });

  it("FIT-002 the fitted corners touch the ground", async (context) => {
    const house = await building(context, "slopeHouse");
    await target(house);
    const after = await fit("BackOnly");
    await bridge.frames(30);
    const points = Back.map((c) => [after.corners[c][0], after.corners[c][2]] as [number, number]);
    const ground = await bridge.heights(points, house);
    Back.forEach((c, i) => expect(Math.abs(after.corners[c][1] - ground[i]), `corner ${c}`).toBeLessThan(0.6));
  });

  it("FIT-003 one undo takes a fit back", async (context) => {
    await target(await building(context, "slopeHouse"));
    await fit("BackOnly");
    await bridge.undo();
    expect((await bridge.settled()).deformed).toBe(false);
  });

  it("FIT-006 back-only fit lifts the back onto ground rising behind the lot", async (context) => {
    await target(await building(context, "risingSlopeHouse"));
    const before = (await bridge.session()).cage!;
    const after = await fit("BackOnly");
    const moved = offsets(after.targetOffsets);
    expect(Back.some((c) => moved[c][1] > 0.5), "the back rises with the slope").toBe(true);
    for (const c of Front) expect(Math.abs(moved[c][1]), "the street side stays at road level").toBeLessThan(0.5);
    columns(after).forEach((h, i) => expect(h).toBeCloseTo(columns(before)[i], 1));
  });

  it("FIT-005 all-corners mode fits the street side too, and the mode stays for other buildings", async (context) => {
    const house = await building(context, "slopeHouse");
    await target(house);
    const log = LogWatch.fromNow();
    const after = await fit("AllCorners");
    await log.waitFor(/\[Edit\] Ground fit mode AllCorners/);
    const moved = offsets(after.targetOffsets);
    expect([...Back, ...Front].filter((c) => Math.abs(moved[c][1]) > 0.05).length).toBeGreaterThanOrEqual(3);
    await bridge.stepBack();
    await target(await building(context, "plainHouse"));
    expect((await bridge.session()).groundFitMode).toBe("AllCorners");
  });

  it("FIT-004 after a fit a lifted lot follows the slope and a flat one keeps a level pad", async (context) => {
    const house = await building(context, "slopeHouse");
    await target(house);
    await fit("BackOnly");
    await bridge.frames(30); // the terrain pass runs a few frames after the swap
    const lifted = await centreLine(house);
    expect(lifted.at(-1)! - lifted[0], `the lot falls to the back: ${lifted.join(", ")}`).toBeGreaterThan(1);
    expect((await bridge.toggleTerrain()).result.result).toBe("Accepted");
    await bridge.settled();
    await bridge.frames(30);
    const flat = await centreLine(house);
    expect(Math.max(...flat) - Math.min(...flat), `a level pad: ${flat.join(", ")}`).toBeLessThan(0.5);
  });
});
