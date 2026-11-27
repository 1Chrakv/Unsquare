import { beforeAll, describe, expect, it } from "vitest";
import { bridge } from "../../src/bridge.js";
import { corner } from "../../src/expect.js";
import { building, cleanUp, loadFixture } from "../../src/fixture.js";
import { move, target } from "../../src/setup.js";

/** In-game days a simulation test runs: a day takes 10–20 minutes even at full speed, so the suite runs a quarter. */
const Days = Number(process.env.UNSQUARE_DAYS ?? 0.25);

describe("ZONING – deformed growables in the simulation", () => {
  beforeAll(async () => {
    await loadFixture({ fresh: true });
    await cleanUp();
  });

  it("ZONING-001 a deformed growable keeps its residents and level while the city runs", async (context) => {
    const growable = await building(context, "growableL1");
    const before = await bridge.info(growable);
    await target(growable);
    await move("Corner", corner(1, 1, 1), [1, 0, 0]);
    const spec = (await bridge.settled()).targetSpec;
    await bridge.stepBack();
    await bridge.stepBack();
    await bridge.simulate({ days: Days });
    const after = await bridge.info(growable);
    expect(after.condemned, "condemned").toBe(false);
    expect(after.abandoned, "abandoned").toBe(false);
    expect(after.level).toBe(before.level);
    if (before.renters > 0) expect(after.renters, "still has residents").toBeGreaterThan(0);
    expect((await bridge.settled(growable)).targetSpec, "still deformed").toBe(spec);
  }, 1_800_000);

  it("ZONING-002 rezoning a neighbouring cell leaves a deformed growable standing, zoned and lived in", async (context) => {
    await loadFixture({ fresh: true });
    const growable = await building(context, "growableL1");
    const { lotSize } = await bridge.building(growable);
    const zoneBefore = (await bridge.zone(growable)).inside;
    const renters = (await bridge.info(growable)).renters;
    await target(growable);
    await move("Corner", corner(1, 1, 1), [1, 0, 0]);
    await bridge.stepBack();
    await bridge.stepBack();
    // The first cell beside the lot's left side, at the street.
    const [width, depth] = lotSize!;
    const beside: [number, number] = [width * 4 + 4, depth * 4 - 4];
    const painted = await bridge.paintZone({ building: growable, zone: "None", points: [beside], radius: 3 });
    expect(painted.result.cells, "a neighbouring cell was unzoned").toBeGreaterThan(0);
    await bridge.simulate({ frames: 4096 });
    const after = await bridge.info(growable);
    expect(after.condemned, "no condemned icon").toBe(false);
    expect((await bridge.zone(growable)).inside, "its zone stays under it").toEqual(zoneBefore);
    if (renters > 0) expect(after.renters, "residents stay").toBeGreaterThan(0);

    await target(growable);
    await bridge.reset();
    expect((await bridge.settled()).deformed).toBe(false);
    await bridge.stepBack();
    await bridge.simulate({ frames: 4096 });
    const reset = await bridge.info(growable); // throws if the building was removed
    expect(reset.condemned, "after a reset it still stands").toBe(false);
  }, 1_200_000);
});
