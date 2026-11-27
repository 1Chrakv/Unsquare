import { describe, expect, it } from "vitest";
import { bridge } from "../src/bridge.js";
import { Face } from "../src/expect.js";
import { building } from "../src/fixture.js";
import { LogWatch } from "../src/log.js";
import { target, useCity } from "../src/setup.js";

describe("MODE – Unsquare mode", () => {
  useCity();

  it("MODE-004 leaving the mode during a drag cancels it and clears the selection", async (context) => {
    const house = await building(context, "plainHouse");
    await target(house, "Face", Face.top);
    await bridge.drag({ translate: [0, 3, 0], frames: 5, hold: true });
    const log = LogWatch.fromNow();
    await bridge.mode(false);
    await log.waitFor(/\[Edit\] Unsquare mode off/);
    const session = await bridge.session();
    expect(session.dragging).toBe(false);
    expect(session.target).toBeNull();
    expect((await bridge.settled(house)).deformed, "the drag's shape is dropped").toBe(false);
    await bridge.mode(true);
  });
});
