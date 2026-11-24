import { describe, expect, it } from "vitest";
import { bridge } from "../src/bridge.js";
import { Face, offsets } from "../src/expect.js";
import { building, loadFixture } from "../src/fixture.js";
import { LogWatch } from "../src/log.js";
import { target, twoOfAKind, useCity } from "../src/setup.js";

const topHeight = async () => offsets((await bridge.settled()).targetOffsets)[7][1];

describe("UNDO – undo and redo", () => {
  useCity();

  it("UNDO-001 each undo takes back one nudge, redo repeats it, the selection stays", async (context) => {
    await target(await building(context, "plainHouse"), "Face", Face.top);
    for (let i = 0; i < 3; i++) {
      await bridge.nudge([0, 1, 0]);
      await bridge.settled();
    }
    expect(await topHeight()).toBeCloseTo(0.3, 5);
    const log = LogWatch.fromNow();
    for (let i = 0; i < 3; i++) await bridge.undo();
    expect(await topHeight()).toBeCloseTo(0, 5);
    expect(log.matching(/\[Edit\] Undo on /)).toHaveLength(3);
    await bridge.redo();
    expect(await topHeight()).toBeCloseTo(0.1, 5);
    expect((await bridge.session()).handle).toBe("Face3");
  });

  it("UNDO-002 a whole drag is undone in one step", async (context) => {
    await target(await building(context, "plainHouse"), "Face", Face.top);
    await bridge.drag({ translate: [0, 3, 0], frames: 20 });
    expect(await topHeight()).toBeCloseTo(3, 5);
    await bridge.undo();
    expect(await topHeight()).toBeCloseTo(0, 5);
    expect((await bridge.session()).canUndo).toBe(false);
  });

  it("UNDO-003 undoing another building's edit makes it the target and drops the selection", async (context) => {
    const pair = await twoOfAKind();
    if (!pair) return context.skip();
    const [a, b] = pair;
    await target(a.ref, "Face", Face.top);
    await bridge.nudge([0, 1, 0]);
    await bridge.settled();
    await bridge.stepBack();
    await bridge.stepBack();
    await target(b.ref, "Face", Face.top);

    await bridge.undo();
    const session = await bridge.session();
    expect(session.target?.ref).toBe(a.ref);
    expect(session.handle).toBeNull();
    expect((await bridge.settled(a.ref)).deformed).toBe(false);
  });

  it("UNDO-004 edits of a bulldozed building are skipped; the older edit is undone", async (context) => {
    const pair = await twoOfAKind();
    if (!pair) return context.skip();
    const [a, b] = pair;
    for (const house of [a, b]) {
      await target(house.ref, "Face", Face.top);
      await bridge.nudge([0, 1, 0]);
      await bridge.settled();
      await bridge.stepBack();
      await bridge.stepBack();
    }
    await bridge.bulldoze(b.ref);
    await bridge.frames(10);
    await bridge.undo();
    const session = await bridge.session();
    expect(session.target?.ref).toBe(a.ref);
    expect((await bridge.settled(a.ref)).deformed).toBe(false);
    // The fixture lost a building: reload it for the next spec file.
    await loadFixture({ fresh: true });
  });

  it("UNDO-005 after reset all there is nothing to undo", async (context) => {
    await target(await building(context, "plainHouse"), "Face", Face.top);
    await bridge.nudge([0, 1, 0]);
    await bridge.settled();
    await bridge.resetAll();
    const log = LogWatch.fromNow();
    await bridge.undo();
    await log.waitFor(/\[Edit\] Nothing to undo/);
    expect((await bridge.session()).problem).toBe("NothingToUndo");
  });

  it("UNDO-006 a handle reset is undone in one step", async (context) => {
    await target(await building(context, "plainHouse"), "Face", Face.top);
    await bridge.nudge([0, 1, 0]);
    await bridge.nudge([0, 1, 0]);
    await bridge.settled();
    await bridge.resetHandle();
    expect(await topHeight()).toBeCloseTo(0, 5);
    await bridge.undo();
    expect(await topHeight()).toBeCloseTo(0.2, 5);
  });
});
