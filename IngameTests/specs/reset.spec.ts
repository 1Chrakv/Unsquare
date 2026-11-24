import { describe, expect, it } from "vitest";
import { bridge } from "../src/bridge.js";
import { expectUndeformed, Face, offsets } from "../src/expect.js";
import { building } from "../src/fixture.js";
import { LogWatch } from "../src/log.js";
import { refused, target, twoOfAKind, useCity } from "../src/setup.js";

describe("RESET – resets", () => {
  useCity();

  it("RESET-001 reset restores the building; reset all resets every building and logs the count", async (context) => {
    const pair = await twoOfAKind();
    if (!pair) return context.skip();
    for (const house of pair) {
      await bridge.stepBack();
      await target(house.ref, "Face", Face.top);
      await bridge.nudge([0, 1, 0]);
      await bridge.settled();
    }
    await bridge.reset();
    expectUndeformed(await bridge.settled());

    await bridge.stepBack();
    await target(pair[0].ref, "Face", Face.top);
    await bridge.nudge([0, 1, 0]);
    await bridge.settled();
    const log = LogWatch.fromNow();
    expect(Number((await bridge.resetAll()).result.result)).toBeGreaterThanOrEqual(1);
    await log.waitFor(/\[Edit\] Reset all: \d+ buildings/);
    expectUndeformed(await bridge.settled(pair[0].ref));
  });

  it("RESET-002 resetting a corner drops only that corner back", async (context) => {
    await target(await building(context, "plainHouse"), "Face", Face.top);
    await bridge.nudge([0, 1, 0]);
    await bridge.settled();
    await bridge.handle("Corner", 7);
    const log = LogWatch.fromNow();
    await bridge.resetHandle();
    const corners = offsets((await bridge.settled()).targetOffsets);
    expect(corners[7]).toEqual([0, 0, 0]);
    for (const corner of [2, 3, 6]) expect(corners[corner][1]).toBeCloseTo(0.1, 5);
    await log.waitFor(/\[Edit\] Reset Corner7 -> Accepted/);
  });

  it("RESET-003 reset handle without a handle changes nothing", async (context) => {
    await target(await building(context, "plainHouse"));
    const log = LogWatch.fromNow();
    expect(await refused(bridge.resetHandle())).toBe("NotDeformable");
    await log.waitFor(/\[Edit\] No handle selected/);
    expect((await bridge.session()).cage!.deformed).toBe(false);
  });
});
