import { afterEach, describe, expect, it } from "vitest";
import { bridge } from "../src/bridge.js";
import { corner, offsets } from "../src/expect.js";
import { building } from "../src/fixture.js";
import { LogWatch } from "../src/log.js";
import { refused, target, useCity } from "../src/setup.js";

const Bottom = { leftBack: corner(1, 0, 0), leftFront: corner(1, 0, 1), rightBack: corner(0, 0, 0), rightFront: corner(0, 0, 1) };

async function level(side: "Left" | "Back" | "Front") {
  const log = LogWatch.fromNow();
  expect((await bridge.level(side)).result.result).toBe("Accepted");
  await log.waitFor(new RegExp(`Levelling ${side} of building`));
  return offsets((await bridge.settled()).targetOffsets);
}

describe("LEVEL – level with neighbour", () => {
  useCity();
  let reach: unknown;
  afterEach(async () => {
    if (reach !== undefined) await bridge.setSettings({ AlignReach: reach });
    reach = undefined;
  });

  it("LEVEL-001 levelling the left side moves only its bottom corners, up to the higher neighbour", async (context) => {
    await target(await building(context, "raisedNeighbour"));
    const after = await level("Left");
    expect(after[Bottom.leftBack][1]).toBeGreaterThan(1);
    expect(after[Bottom.leftFront][1]).toBeGreaterThan(1);
    for (const c of [Bottom.rightBack, Bottom.rightFront, 2, 3, 6, 7]) expect(after[c], `corner ${c}`).toEqual([0, 0, 0]);
  });

  it("LEVEL-002 the back side at a sloped road goes to the road surface per corner", async (context) => {
    await target(await building(context, "backRoad"));
    const after = await level("Back");
    expect(after[Bottom.leftBack][1] !== 0 || after[Bottom.rightBack][1] !== 0).toBe(true);
    expect(after[Bottom.leftFront]).toEqual([0, 0, 0]);
  });

  it("LEVEL-003 without a neighbour levelling is refused; a level is one undo step", async (context) => {
    await target(await building(context, "plainHouse"));
    reach = (await bridge.settings()).AlignReach;
    await bridge.setSettings({ AlignReach: 2 });
    const log = LogWatch.fromNow();
    expect(await refused(bridge.level("Left"))).toBe("NoNeighbour");
    await log.waitFor(/no neighbour within/);
  });

  it("LEVEL-004 the street side at a sloped street goes to the road surface; the back stays", async (context) => {
    await target(await building(context, "slopedRoad"));
    const after = await level("Front");
    expect(after[Bottom.leftFront][1]).not.toBeCloseTo(after[Bottom.rightFront][1], 1);
    expect(after[Bottom.leftBack]).toEqual([0, 0, 0]);
    await bridge.undo();
    expect((await bridge.settled()).deformed).toBe(false);
  });

  it("LEVEL-005 levelling towards a free footpath moves that side to the path surface", async (context) => {
    await target(await building(context, "footpathHouse"));
    await level("Left");
  });
});
