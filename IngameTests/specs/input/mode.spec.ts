import { describe, expect, it } from "vitest";
import { bridge } from "../../src/bridge.js";
import { Face } from "../../src/expect.js";
import { building } from "../../src/fixture.js";
import { centreOf, click, press, tap, useInputCity } from "../../src/input.js";
import { LogWatch } from "../../src/log.js";
import { target } from "../../src/setup.js";
import { button, clickButton, evaluate } from "../../src/ui.js";
import { lookAt } from "../../src/view.js";

const pauseMenuOpen = () => evaluate<boolean>(`/Load Game/.test(document.body.textContent)`);

describe("MODE – Unsquare mode (real input)", () => {
  useInputCity();

  it("MODE-001 Alt+U and the toolbar button toggle the mode, highlight the button and log it", async () => {
    try {
      for (const [how, toggle] of [["Alt+U", () => press("UnsquareToggleMode")], ["button", () => clickButton("unsquare-mode")]] as const) {
        for (const on of [false, true]) {
          const log = LogWatch.fromNow();
          await toggle();
          expect((await bridge.session()).mode, `${how} switches the mode ${on ? "on" : "off"}`).toBe(on);
          await log.waitFor(new RegExp(`\\[Edit\\] Unsquare mode ${on ? "on" : "off"}`));
          expect((await button("unsquare-mode"))?.active, "button highlighted while on").toBe(on);
        }
      }
    } finally {
      if (!(await bridge.session()).mode) await bridge.mode(true);
    }
  });

  it("MODE-002 outside the mode hotkeys, undo and clicks do nothing; a click opens the info panel", async (context) => {
    const house = await building(context, "plainHouse");
    await lookAt(house, "front", 120);
    await bridge.mode(false);
    try {
      const log = LogWatch.fromNow();
      await press("UnsquareNextHandle");
      await press("UnsquareUndo");
      await press("UnsquareResetAll");
      await click(await centreOf(house));
      await bridge.frames(10);
      expect(log.matching(/\[Edit\]/), "no edit command ran").toEqual([]);
      expect((await bridge.session()).target).toBeNull();
      expect((await bridge.state()).result.selected, "the game's info panel shows the building").toBe((await bridge.building(house)).id);
    } finally {
      await bridge.infoPanel();
      await bridge.mode(true);
    }
  });

  it("MODE-003 Esc first leaves the mode without stepping back, then opens the pause menu", async (context) => {
    const house = await building(context, "plainHouse");
    await target(house, "Face", Face.top);
    try {
      const log = LogWatch.fromNow();
      await tap("Escape");
      expect((await bridge.session()).mode).toBe(false);
      await log.waitFor(/\[Edit\] Unsquare mode off/);
      // Leaving the mode drops the target in one go; a step back would first clear the selected handle.
      expect(log.matching(/\[Edit\] Selection cleared/), "Esc never steps back").toEqual([]);
      expect(await pauseMenuOpen()).toBe(false);
      await bridge.key({ keys: ["Escape"] }, { settleFrames: 10 });
      expect(await pauseMenuOpen(), "the second Esc opens the pause menu").toBe(true);
    } finally {
      if (await pauseMenuOpen()) await bridge.key({ keys: ["Escape"] }, { settleFrames: 10 });
      await bridge.mode(true);
    }
  });
});
