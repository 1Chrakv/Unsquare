import { describe, expect, it } from "vitest";
import { bridge, type Vec3 } from "../src/bridge.js";
import { corner, edge } from "../src/expect.js";
import { building } from "../src/fixture.js";
import { LogWatch } from "../src/log.js";
import { move, target, useCity } from "../src/setup.js";

const round = (points: Vec3[]) => points.map((p) => p.map((v) => Math.round(v * 20) / 20).join(",")).sort();

/** Areas, path nodes and props of a building, rounded for comparison. */
async function layout(ref: string) {
  return {
    areas: round((await bridge.areas(ref)).flatMap((a) => a.local)),
    nets: round((await bridge.nets(ref)).nodes.map((n) => n.local)),
    props: round((await bridge.subObjects(ref)).map((p) => p.local)),
  };
}

describe("LOT – lot deformation", () => {
  useCity();

  it("LOT-002 resetting a lot-deformed building restores the original layout", async (context) => {
    const house = await building(context, "gardenHouse");
    await target(house);
    const before = await layout(house);
    await move("Corner", corner(1, 0, 1), [1.5, 0, 0]);
    await move("Corner", corner(0, 0, 0), [0, 0, -1]);
    expect(await layout(house)).not.toEqual(before);
    await bridge.reset();
    await bridge.settled();
    expect(await layout(house)).toEqual(before);
  });

  it("LOT-003 a park's benches, trees and paths follow its cage", async (context) => {
    const park = await building(context, "park");
    await target(park);
    const log = LogWatch.fromNow();
    await bridge.handle("Edge", edge(corner(0, 1, 1), corner(1, 1, 1)));
    await bridge.drag({ translate: [0, 2, 0], frames: 4 });
    await move("Corner", corner(1, 0, 1), [1.5, 0, 0]);
    expect((await bridge.settled()).status).toBe("Applied");
    expect(log.matching(/has no lot cage/)).toHaveLength(0);
    for (const node of (await bridge.nets(park)).nodes) expect(node.edges, `path node ${node.id}`).toBeGreaterThan(0);
  });

  it("LOT-005 dragging a park's street side slides path ends along the sidewalk; reset brings them back", async (context) => {
    const park = await building(context, "park");
    await target(park);
    const before = await layout(park);
    const log = LogWatch.fromNow();
    await move("Face", 5, [2, 0, 0]);
    const sync = await log.waitFor(/Lot sync for building .*, (\d+) road ends slid/);
    expect(Number(/(\d+) road ends slid/.exec(sync)![1])).toBeGreaterThan(0);
    expect(log.matching(/could not be remapped/)).toHaveLength(0);
    for (const node of (await bridge.nets(park)).nodes) expect(node.edges).toBeGreaterThan(0);
    await bridge.reset();
    await bridge.settled();
    expect((await layout(park)).nets).toEqual(before.nets);
  });
});
