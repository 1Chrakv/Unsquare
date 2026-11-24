import { describe, expect, it } from "vitest";
import { bridge, type Vec3 } from "../src/bridge.js";
import { corner, edge, expectApplied, expectNear, Face, offsets } from "../src/expect.js";
import { building } from "../src/fixture.js";
import { LogWatch } from "../src/log.js";
import { bakes, target, useCity } from "../src/setup.js";

const Top = [2, 3, 6, 7];

/** Turns a building-space point around the vertical axis through `pivot` (counter-clockwise seen from above). */
function turnY(point: Vec3, pivot: Vec3, degrees: number): Vec3 {
  const r = (degrees * Math.PI) / 180;
  const x = point[0] - pivot[0];
  const z = point[2] - pivot[2];
  return [pivot[0] + x * Math.cos(r) + z * Math.sin(r), point[1], pivot[2] - x * Math.sin(r) + z * Math.cos(r)];
}

const centreOf = (points: Vec3[]): Vec3 =>
  [0, 1, 2].map((a) => points.reduce((s, p) => s + p[a], 0) / points.length) as Vec3;

describe("DRAG – dragging", () => {
  useCity();

  it("DRAG-001 dragging the top face up raises the roof and rebakes right at release", async (context) => {
    await target(await building(context, "plainHouse"), "Face", Face.top);
    const log = LogWatch.fromNow();
    const reply = await bridge.drag({ translate: [0, 3, 0], frames: 15 });
    expect(reply.result.result).toBe("Accepted");
    const cage = await bridge.settled(undefined, 120); // no rebake delay after a release
    expectApplied(cage);
    for (const c of Top) expect(offsets(cage.targetOffsets)[c][1]).toBeCloseTo(3, 5);
    expect(log.matching(/\[Edit\] Drag of Face3 ended -> /)).toHaveLength(1);
    expect(bakes(log)).toHaveLength(1);
  });

  it("DRAG-002 dragging an edge moves both its corners together", async (context) => {
    await target(await building(context, "plainHouse"), "Edge", edge(corner(0, 1, 1), corner(1, 1, 1)));
    await bridge.drag({ translate: [2, 0, 0], frames: 5 });
    await bridge.drag({ translate: [0, 0, 1], frames: 5 });
    const corners = offsets((await bridge.settled()).targetOffsets);
    for (const c of [6, 7]) expect(corners[c]).toEqual([2, 0, 1]);
    for (const c of [0, 1, 2, 3, 4, 5]) expect(corners[c]).toEqual([0, 0, 0]);
  });

  it("DRAG-003 a right-click during a drag or a turn snaps the shape back", async (context) => {
    await target(await building(context, "plainHouse"), "Face", Face.top);
    await bridge.drag({ translate: [0, 1, 0], frames: 3 });
    const before = (await bridge.settled()).targetOffsets;
    const log = LogWatch.fromNow();
    expect((await bridge.drag({ translate: [0, 4, 0], frames: 10, cancelAt: 6 })).result.cancelled).toBe(true);
    expect((await bridge.settled()).targetOffsets).toBe(before);
    await bridge.drag({ rotate: { axis: 1, degrees: 25 }, frames: 10, cancelAt: 5 });
    expect((await bridge.settled()).targetOffsets).toBe(before);
    expect(log.matching(/\[Edit\] Drag of Face3 cancelled/)).toHaveLength(2);
    expect((await bridge.session()).canUndo).toBe(true); // only the first drag is in the history
    await bridge.undo();
    expect((await bridge.settled()).deformed).toBe(false);
  });

  it("DRAG-007 the top face's vertical ring twists the top around its centre in one undo step", async (context) => {
    await target(await building(context, "plainHouse"), "Face", Face.top);
    const start = (await bridge.session()).cage!;
    const pivot = centreOf(Top.map((c) => start.corners[c]));
    await bridge.drag({ rotate: { axis: 1, degrees: 20 }, frames: 10 });
    const cage = await bridge.settled();
    for (const c of Top) expectNear(cage.corners[c], turnY(start.corners[c], pivot, 20), 0.1, `corner ${c}`);
    for (const c of [0, 1, 4, 5]) expect(cage.corners[c]).toEqual(start.corners[c]);
    await bridge.undo();
    expect((await bridge.settled()).deformed).toBe(false);
  });

  it("DRAG-009 a turn that folds the cage is refused", async (context) => {
    await target(await building(context, "plainHouse"), "Face", Face.top);
    await bridge.drag({ rotate: { axis: 0, degrees: 175 }, frames: 10 });
    const session = await bridge.session();
    expect(session.problem).toBe("CageFolds");
    expect((await bridge.settled()).deformed, "the folding turn is not applied").toBe(false);
  });

  it("DRAG-010 dragging the whole cage shifts building, props and lot together", async (context) => {
    const house = await building(context, "plainHouse");
    await target(house, "Cage");
    const props = await bridge.subObjects(house);
    await bridge.drag({ translate: [3, 0, 0], frames: 10 });
    const cage = await bridge.settled();
    for (const c of offsets(cage.targetOffsets)) expect(c).toEqual([3, 0, 0]);
    expect(cage.split).toBe(false);
    const moved = await bridge.subObjects(house);
    for (const prop of props) {
      const expected: Vec3 = [prop.local[0] + 3, prop.local[1], prop.local[2]];
      const nearest = moved.filter((m) => m.prefab === prop.prefab).sort((a, b) => dist(a.local, expected) - dist(b.local, expected))[0];
      expect(nearest, `prop ${prop.prefab} still there`).toBeDefined();
      expect(dist([nearest.local[0], 0, nearest.local[2]], [expected[0], 0, expected[2]]), `prop ${prop.prefab}`).toBeLessThan(0.3);
    }
    await bridge.undo();
    expect((await bridge.settled()).deformed).toBe(false);
  });

  it("DRAG-011 whole-cage drags follow the edit mode", async (context) => {
    await target(await building(context, "plainHouse"));
    await bridge.editMode("Building");
    await bridge.handle("Cage");
    await bridge.drag({ translate: [1, 0, 0], frames: 5 });
    await bridge.settled();
    await bridge.editMode("Lot");
    expect((await bridge.session()).cage!.deformed, "Building mode leaves the lot").toBe(false);
    await bridge.undo();
    await bridge.settled();

    await bridge.handle("Cage");
    await bridge.drag({ translate: [1, 0, 0], frames: 5 });
    await bridge.settled();
    await bridge.editMode("Building");
    expect((await bridge.session()).cage!.deformed, "Lot mode leaves the building").toBe(false);
  });

  it("DRAG-012 the whole cage's vertical ring turns the building in place", async (context) => {
    await target(await building(context, "plainHouse"), "Cage");
    const start = (await bridge.session()).cage!;
    const pivot = centreOf(start.corners);
    await bridge.drag({ rotate: { axis: 1, degrees: 15 }, frames: 10 });
    const cage = await bridge.settled();
    for (let c = 0; c < 8; c++) expectNear(cage.corners[c], turnY(start.corners[c], pivot, 15), 0.1, `corner ${c}`);
  });

  it("DRAG-013 dragging the whole cage far away stops at the max offset", async (context) => {
    await target(await building(context, "plainHouse"), "Cage");
    const reply = await bridge.drag({ translate: [400, 0, 0], frames: 10 });
    expect(reply.result.result).toBe("Clamped");
    const x = offsets((await bridge.settled()).targetOffsets)[0][0];
    expect(x).toBeGreaterThan(0);
    expect(x).toBeLessThan(400);
  });
});

const dist = (a: Vec3, b: Vec3) => Math.hypot(a[0] - b[0], a[1] - b[1], a[2] - b[2]);
