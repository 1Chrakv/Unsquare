import { describe, expect, it } from "vitest";
import { bridge, type Vec3 } from "../src/bridge.js";
import { corner, edge } from "../src/expect.js";
import { building } from "../src/fixture.js";
import { LogWatch } from "../src/log.js";
import { move, target, useCity } from "../src/setup.js";
import { pack } from "../src/review.js";
import { lookAt, shot } from "../src/view.js";

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

  it("LOT-001 a bottom corner outside the plot turns the cage magenta and warns once; the edit applies", async (context) => {
    const house = await building(context, "plainHouse");
    await target(house);
    const log = LogWatch.fromNow();
    const cage = await move("Corner", corner(1, 0, 1), [3, 0, 0]);
    expect(cage.exceedsLot, "the lot outline leaves its plot").toBe(true);
    expect(cage.status, "the edit still applies").toBe("Applied");
    await move("Corner", corner(1, 0, 1), [0.5, 0, 0]);
    expect(log.matching(/Lot outline leaves its plot/), "warned once").toHaveLength(1);
    await bridge.clearHandle();
    await lookAt(house, "top");
    await pack("LOT-001", {
      pack: "overlay",
      shots: { outside: await shot("LOT-001-outside", { crop: "target" }) },
      probe: { exceedsLot: cage.exceedsLot, offsets: cage.targetOffsets },
      focus: "The cage is drawn magenta because one bottom corner sticks out of the plot.",
    });
  });

  it("LOT-004 curved park paths stay connected through two deformations; reset restores the park (with a ground review)", async (context) => {
    const park = await building(context, "park");
    await target(park);
    const before = await layout(park);
    await lookAt(park, "top");
    const original = await shot("LOT-004-original", { crop: "target", ui: false });
    const log = LogWatch.fromNow();
    await move("Corner", corner(1, 0, 1), [2, 0, 1]);
    await move("Corner", corner(0, 0, 0), [-1.5, 0, -1]);
    const syncs = log.matching(/Lot sync for building .*\d+ net edges moved/);
    expect(syncs.length, "the paths were synced").toBeGreaterThan(0);
    expect(log.matching(/could not be remapped/), "no unmapped parts").toEqual([]);
    for (const node of (await bridge.nets(park)).nodes) expect(node.edges, `path node ${node.id} connected`).toBeGreaterThan(0);
    await bridge.clearHandle();
    const deformed = await shot("LOT-004-deformed", { crop: "target", ui: false });
    await bridge.reset();
    await bridge.settled();
    await bridge.frames(60); // the lot sync of the reset lands a few frames after the swap
    // In plan: ground items are projected onto the terrain again, which can move their height by a rounding step.
    const plan = (l: Awaited<ReturnType<typeof layout>>) =>
      Object.fromEntries(Object.entries(l).map(([k, points]) => [k, points.map((p) => p.split(",").filter((_, i) => i !== 1).join(",")).sort()]));
    expect(plan(await layout(park)), "reset gives the original layout").toEqual(plan(before));
    await pack("LOT-004", {
      pack: "ground",
      shots: { original, deformed },
      probe: { syncs },
      log,
      focus: "The park's curved paths in the deformed shot bend smoothly with the new outline; the road entrance stays attached.",
    });
  });
});
