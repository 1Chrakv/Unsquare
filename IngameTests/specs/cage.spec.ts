import { describe, expect, it } from "vitest";
import { bridge } from "../src/bridge.js";
import { building } from "../src/fixture.js";
import { target, useCity } from "../src/setup.js";

const LotCell = 8;

describe("CAGE – cage geometry", () => {
  useCity();

  it("CAGE-001 rest box and cage line up with buildings at any rotation", async (context) => {
    const candidates = (await bridge.find({ variant: false, upgrades: false, limit: 200 })).filter(
      (b) => b.deformable && b.lotSize,
    );
    // Buildings facing different directions: one per 30° sector.
    const bySector = new Map<number, (typeof candidates)[number]>();
    for (const b of candidates) bySector.set(Math.floor(((b.yawDegrees + 360) % 360) / 30), b);
    if (bySector.size < 2) return context.skip();
    for (const house of [...bySector.values()].slice(0, 4)) {
      const cage = (await bridge.building(house.ref)).cage!;
      const [cellsX, cellsZ] = house.lotSize!;
      // Building space: the lot box is centred on the building and as large as its lot.
      expect(cage.lotBox.max[0] - cage.lotBox.min[0], `${house.ref} width`).toBeCloseTo(cellsX * LotCell, 1);
      expect(cage.lotBox.max[2] - cage.lotBox.min[2], `${house.ref} depth`).toBeCloseTo(cellsZ * LotCell, 1);
      expect(Math.abs(cage.lotBox.min[0] + cage.lotBox.max[0]), `${house.ref} centred in x`).toBeLessThan(0.5);
      expect(cage.corners[0]).toEqual(cage.box.min);
      expect(cage.corners[7]).toEqual(cage.box.max);
    }
  });

  it("CAGE-002 the cage spans the lot and is at least 8 m tall", async (context) => {
    const park = await building(context, "park");
    const cage = (await bridge.building(park)).cage!;
    expect(cage.lotBox.min[1]).toBe(0);
    expect(cage.lotBox.max[1] - cage.lotBox.min[1]).toBeGreaterThanOrEqual(8 - 1e-3);
  });

  it("CAGE-003 with a storey upgrade the cage reaches the storey's top; without, the roof", async (context) => {
    const carPark = await building(context, "carPark");
    await target(carPark);
    const raised = (await bridge.session()).cage!;
    expect(raised.box.max[1], "edit box raised above the lot box").toBeGreaterThan(raised.lotBox.max[1] + 1);

    await bridge.stepBack();
    await target(await building(context, "plainHouse"));
    const plain = (await bridge.session()).cage!;
    expect(plain.box.max[1]).toBeCloseTo(plain.lotBox.max[1], 2);
  });
});
