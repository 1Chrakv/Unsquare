import { describe, expect, it } from "vitest";
import { bridge } from "../src/bridge.js";
import { Face } from "../src/expect.js";
import { building } from "../src/fixture.js";
import { LogWatch } from "../src/log.js";
import { refused, target, twoOfAKind, useCity } from "../src/setup.js";

describe("SEL – selecting buildings and handles", () => {
  useCity();

  it("SEL-003 a selected handle locks the building until the selection is cleared", async (context) => {
    const pair = await twoOfAKind();
    if (!pair) return context.skip();
    await target(pair[0].ref, "Edge", 0);
    await refused(bridge.select(pair[1].ref));
    expect((await bridge.session()).target?.ref).toBe(pair[0].ref);
    const log = LogWatch.fromNow();
    expect((await bridge.stepBack()).result.result).toBe("SelectionCleared");
    await log.waitFor(/\[Edit\] Selection cleared/);
    await bridge.select(pair[1].ref);
    expect((await bridge.session()).target?.ref).toBe(pair[1].ref);
  });

  it("SEL-004 stepping back clears the drag, then the handle, then the target, never the mode", async (context) => {
    await target(await building(context, "plainHouse"), "Face", Face.top);
    await bridge.drag({ translate: [0, 1, 0], frames: 3, hold: true });
    const results: string[] = [];
    for (let i = 0; i < 4; i++) results.push((await bridge.stepBack()).result.result);
    expect(results).toEqual(["DragCancelled", "SelectionCleared", "TargetCleared", "None"]);
    const session = await bridge.session();
    expect(session.mode).toBe(true);
    expect(session.target).toBeNull();
  });

  it("SEL-009 a reselected deformed building continues from its current shape", async (context) => {
    const house = await building(context, "plainHouse");
    await target(house, "Face", Face.top);
    await bridge.drag({ translate: [0, 2, 0], frames: 3 });
    const shaped = (await bridge.settled()).targetOffsets;
    await bridge.stepBack();
    await bridge.stepBack();
    await target(house, "Face", Face.top);
    expect((await bridge.session()).cage!.targetOffsets).toBe(shaped);
    await bridge.nudge([0, 1, 0]);
    const top = (await bridge.settled()).corners[7][1];
    const box = (await bridge.session()).cage!.box;
    expect(top).toBeCloseTo(box.max[1] + 2.1, 1);
  });

  it("SEL-010 describe writes the cage state to the log", async (context) => {
    const house = await building(context, "plainHouse");
    await target(house);
    const log = LogWatch.fromNow();
    await bridge.describe();
    await log.waitFor(/\[Edit\] Building Entity\(\d+:\d+\): Undeformed, Both cage on .*pick Edge, selected none/);
  });
});
