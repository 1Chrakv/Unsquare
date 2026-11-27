import { describe, expect, it } from "vitest";
import { bridge } from "../../src/bridge.js";
import { Face } from "../../src/expect.js";
import { building, loadFixture } from "../../src/fixture.js";
import { centreOf, click, hover, press, useInputCity } from "../../src/input.js";
import { LogWatch } from "../../src/log.js";
import { target } from "../../src/setup.js";
import { lookAt } from "../../src/view.js";

/** Raises the top face of a building by one nudge and leaves it. */
async function raise(ref: string) {
  await target(ref, "Face", Face.top);
  await bridge.nudge([0, 1, 0]);
  await bridge.settled();
  await bridge.stepBack();
  await bridge.stepBack();
}

describe("UNDO – undo after the game's bulldozer", () => {
  useInputCity();

  it("UNDO-004 edits of a bulldozed building are skipped silently; the next older edit is undone", async (context) => {
    const older = await building(context, "sameTypeA");
    const newer = await building(context, "sameTypeB");
    try {
      await raise(older);
      await raise(newer);
      const newerId = (await bridge.building(newer)).id;

      await bridge.mode(false);
      await bridge.tool("bulldoze");
      await lookAt(newer, "front", 90);
      const at = await centreOf(newer);
      await hover(at); // the game's raycast lags a frame behind the pointer
      await click(at);
      await bridge.frames(30);
      await bridge.tool("default");
      const [x, z] = newer.slice(newer.indexOf("@") + 1).split(",").map(Number);
      const left = (await bridge.find({ near: [x, z], radius: 2 })).filter((b) => b.id === newerId);
      expect(left, "the bulldozer removed the newer building").toEqual([]);

      await bridge.mode(true);
      const log = LogWatch.fromNow();
      await press("UnsquareUndo");
      const cage = await bridge.settled(older);
      expect(cage.deformed, "the older building's edit is undone").toBe(false);
      expect(log.matching(/Exception|\[Edit\].*(failed|refused)/i), "skipped silently").toEqual([]);
    } finally {
      // The bulldozer changed the city: start the next test on a fresh copy of the fixture.
      await loadFixture({ fresh: true });
    }
  }, 600_000);
});
