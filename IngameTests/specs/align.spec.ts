import { afterEach, describe, expect, it } from "vitest";
import { bridge } from "../src/bridge.js";
import { corner, distanceXZ, offsets } from "../src/expect.js";
import { building } from "../src/fixture.js";
import { LogWatch } from "../src/log.js";
import { move, refused, target, useCity } from "../src/setup.js";

// The left side seen from the street is the +X face: bottom corners 1 (back) and 5 (street side).
const LeftBack = corner(1, 0, 0);
const LeftFront = corner(1, 0, 1);

async function alignLeft(mode: "Angle" | "Flush" | "Full") {
  await bridge.alignMode(mode);
  const before = (await bridge.session()).cage!;
  const log = LogWatch.fromNow();
  expect((await bridge.align("Left")).result.result).toBe("Accepted");
  const after = await bridge.settled();
  await log.waitFor(new RegExp(`Aligning Left of building .* to .* \\(${mode}\\)`));
  return { before, after, moved: [LeftBack, LeftFront].map((c) => distanceXZ(before.corners[c], after.corners[c]) > 0.05) };
}

describe("ALIGN – align to neighbour", () => {
  useCity();
  let reach: unknown;
  afterEach(async () => {
    if (reach !== undefined) await bridge.setSettings({ AlignReach: reach });
    reach = undefined;
  });

  it("ALIGN-001 angle mode turns the side and keeps the corner nearer the neighbour", async (context) => {
    await target(await building(context, "turnedNeighbour"));
    const { moved } = await alignLeft("Angle");
    expect(moved.filter(Boolean), "exactly one of the side's corners moves").toHaveLength(1);
  });

  it("ALIGN-002 flush mode moves both corners onto the neighbour's edge", async (context) => {
    await target(await building(context, "turnedNeighbour"));
    const { moved } = await alignLeft("Flush");
    expect(moved).toEqual([true, true]);
  });

  it("ALIGN-003 full mode puts the side's corners on the neighbour lot's corners", async (context) => {
    await target(await building(context, "turnedNeighbour"));
    const { moved } = await alignLeft("Full");
    expect(moved.some(Boolean)).toBe(true);
  });

  it("ALIGN-004 the back side turns to a road behind the lot", async (context) => {
    await target(await building(context, "backRoad"));
    await bridge.alignMode("Flush");
    const log = LogWatch.fromNow();
    await bridge.align("Back");
    await log.waitFor(/Aligning Back of building .* to .*road/i);
  });

  it("ALIGN-005 without a neighbour in reach nothing changes", async (context) => {
    await target(await building(context, "plainHouse"));
    reach = (await bridge.settings()).AlignReach;
    await bridge.setSettings({ AlignReach: 2 });
    const log = LogWatch.fromNow();
    expect(await refused(bridge.align("Left"))).toBe("NoNeighbour");
    await log.waitFor(/Align Left of building .*: no neighbour within 2 m/);
    expect((await bridge.session()).cage!.deformed).toBe(false);
  });

  it("ALIGN-006 each align is one undo and redo step; the target stays", async (context) => {
    const house = await building(context, "turnedNeighbour");
    await target(house);
    const { after } = await alignLeft("Angle");
    await bridge.undo();
    expect((await bridge.settled()).deformed).toBe(false);
    await bridge.redo();
    expect((await bridge.settled()).targetOffsets).toBe(after.targetOffsets);
    expect((await bridge.session()).target?.ref).toBe(house);
  });

  it("ALIGN-007 a side dragged into the neighbour lot aligns back onto the facing edge", async (context) => {
    await target(await building(context, "turnedNeighbour"));
    // Where the side goes from its own place: the neighbour's facing edge.
    const { after: facing } = await alignLeft("Flush");
    await bridge.undo();
    await bridge.settled();
    await move("Face", 1, [3, 0, 0]); // 3 m into the left neighbour
    const { after } = await alignLeft("Flush");
    // From inside the neighbour lot it must land on the same edge, not on the neighbour's far side.
    for (const c of [LeftBack, LeftFront])
      expect(distanceXZ(after.corners[c], facing.corners[c]), `corner ${c}`).toBeLessThan(0.2);
  });

  it("ALIGN-009 a side next to a free footpath aligns to the path", async (context) => {
    await target(await building(context, "footpathHouse"));
    await bridge.alignMode("Flush");
    const log = LogWatch.fromNow();
    await bridge.align("Left");
    await log.waitFor(/Aligning Left of building .* to .*path/i);
    expect(offsets((await bridge.settled()).targetOffsets)[LeftFront][0]).not.toBe(0);
  });

  it("ALIGN-010 a path inside the lot is ignored; the side goes to the road beyond", async (context) => {
    await target(await building(context, "pathThroughLot"));
    await bridge.alignMode("Flush");
    const log = LogWatch.fromNow();
    await bridge.align("Left");
    await log.waitFor(/Aligning Left of building .* to .*road/i);
  });
});
