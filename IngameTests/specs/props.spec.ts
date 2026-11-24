import { afterEach, describe, expect, it } from "vitest";
import { bridge, type CageJson, type SubObjectJson, type Vec3 } from "../src/bridge.js";
import { angleDiff, cageMap, corner, Face, yaw } from "../src/expect.js";
import { building } from "../src/fixture.js";
import { LogWatch } from "../src/log.js";
import { move, target, useCity } from "../src/setup.js";

const dist = (a: Vec3, b: Vec3) => Math.hypot(a[0] - b[0], a[1] - b[1], a[2] - b[2]);

/** For each prop before the edit, the prop of the same prefab nearest to where the cage takes it. */
function follow(before: SubObjectJson[], after: SubObjectJson[], start: CageJson, end: CageJson) {
  return before.map((prop) => {
    const expected = cageMap(end, prop.local, start.box, end.corners);
    const match = after
      .filter((p) => p.prefab === prop.prefab)
      .sort((a, b) => dist(a.local, expected) - dist(b.local, expected))[0];
    return { prop, expected, match, error: match ? dist(match.local, expected) : Infinity };
  });
}

describe("PROPS – props and ground items", () => {
  useCity();
  let saved: Record<string, unknown> | undefined;
  afterEach(async () => {
    if (saved) await bridge.setSettings(saved);
    saved = undefined;
  });

  it("PROPS-001 roof props follow the roof; a reset puts them back", async (context) => {
    const shop = await building(context, "storefront");
    await target(shop);
    const start = (await bridge.session()).cage!;
    const before = await bridge.subObjects(shop);
    const roof = before.filter((p) => p.local[1] > start.box.max[1] * 0.7);
    if (roof.length === 0) return context.skip();
    await move("Face", Face.top, [1, 2, 0]);
    const end = await bridge.settled();
    for (const { prop, error } of follow(roof, await bridge.subObjects(shop), start, end))
      expect(error, `${prop.prefab} at ${prop.local}`).toBeLessThan(0.3);

    await bridge.reset();
    await bridge.settled();
    const back = await bridge.subObjects(shop);
    for (const prop of before)
      expect(Math.min(...back.filter((p) => p.prefab === prop.prefab).map((p) => dist(p.local, prop.local))), prop.prefab).toBeLessThan(0.05);
  });

  it("PROPS-002 a storefront turns with its facade", async (context) => {
    const shop = await building(context, "storefront");
    await target(shop);
    const start = (await bridge.session()).cage!;
    const before = await bridge.subObjects(shop);
    const front = before.filter((p) => p.local[2] > start.box.max[2] - 2 && p.local[1] < 6);
    if (front.length === 0) return context.skip();
    await move("Corner", corner(0, 0, 1), [0, 0, -1.5]);
    await move("Corner", corner(0, 1, 1), [0, 0, -1.5]);
    const end = await bridge.settled();
    const facadeTurn = angleDiff(yaw(sub(end.corners[5], end.corners[4])), yaw(sub(start.corners[5], start.corners[4])));
    expect(Math.abs(facadeTurn)).toBeGreaterThan(2);
    for (const { prop, match } of follow(front, await bridge.subObjects(shop), start, end)) {
      const turn = angleDiff(yaw(match!.forward), yaw(prop.forward));
      expect(Math.abs(turn - facadeTurn), `${prop.prefab}: turned ${turn.toFixed(1)}°, facade ${facadeTurn.toFixed(1)}°`).toBeLessThan(3);
    }
  });

  it("PROPS-003 with facade reach 0 a storefront takes the average turn, not the facade's", async (context) => {
    const shop = await building(context, "storefront");
    saved = await bridge.settings();
    await bridge.setSettings({ FacadeReach: 0 });
    await target(shop);
    const start = (await bridge.session()).cage!;
    const before = await bridge.subObjects(shop);
    const front = before.filter((p) => p.local[2] > start.box.max[2] - 2 && p.local[1] < 6);
    if (front.length === 0) return context.skip();
    // A shape no other test bakes: attachment options apply when a variant is created.
    await move("Corner", corner(0, 0, 1), [0, 0, -1.7]);
    const end = await bridge.settled();
    const facadeTurn = angleDiff(yaw(sub(end.corners[5], end.corners[4])), yaw(sub(start.corners[5], start.corners[4])));
    const turns = follow(front, await bridge.subObjects(shop), start, end).map(({ prop, match }) =>
      angleDiff(yaw(match!.forward), yaw(prop.forward)),
    );
    expect(turns.some((turn) => Math.abs(turn - facadeTurn) > 1), `turns ${turns.join(", ")} vs facade ${facadeTurn}`).toBe(true);
  });

  it("PROPS-004 a lot deformation takes garden areas and props along; trees stay upright", async (context) => {
    const house = await building(context, "gardenHouse");
    await target(house);
    const start = (await bridge.session()).cage!;
    const areasBefore = await bridge.areas(house);
    const propsBefore = await bridge.subObjects(house);
    const log = LogWatch.fromNow();
    await move("Corner", corner(0, 0, 1), [1, 0, 0]);
    await move("Corner", corner(1, 0, 1), [-1, 0, 0]);
    const end = await bridge.settled();
    await log.waitFor(/Lot sync for building .*: [1-9]\d* areas/);
    const areasAfter = new Map((await bridge.areas(house)).map((a) => [a.id, a]));
    for (const area of areasBefore) {
      const moved = areasAfter.get(area.id);
      expect(moved, `area ${area.prefab} kept`).toBeDefined();
      area.local.forEach((point, i) => {
        const expected = cageMap(end, point, start.lotBox, end.corners);
        expect(Math.hypot(moved!.local[i][0] - expected[0], moved!.local[i][2] - expected[2]), `${area.prefab} node ${i}`).toBeLessThan(0.3);
      });
    }
    const propsAfter = await bridge.subObjects(house);
    for (const tree of propsAfter.filter((p) => /tree/i.test(p.prefab))) expect(tree.up[1], tree.prefab).toBeGreaterThan(0.99);
    expect(follow(propsBefore, propsAfter, start, end).filter((f) => f.error > 0.5).map((f) => f.prop.prefab)).toEqual([]);
  });

  it("PROPS-005 with ground areas off, paving and grass keep their shape", async (context) => {
    const house = await building(context, "gardenHouse");
    saved = await bridge.settings();
    await bridge.setSettings({ DeformAreas: false });
    await target(house);
    const before = await bridge.areas(house);
    const log = LogWatch.fromNow();
    await move("Corner", corner(1, 0, 1), [-1.3, 0, 0]);
    await log.waitFor(/Attachments of .*Areas off/);
    expect((await bridge.areas(house)).map((a) => a.local)).toEqual(before.map((a) => a.local));
  });
});

const sub = (a: Vec3, b: Vec3): Vec3 => [a[0] - b[0], a[1] - b[1], a[2] - b[2]];
