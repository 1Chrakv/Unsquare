import { describe, expect, it } from "vitest";
import { bridge } from "../../src/bridge.js";
import { building } from "../../src/fixture.js";
import { centreOf, click, hover, pointOf, useInputCity } from "../../src/input.js";
import { LogWatch } from "../../src/log.js";
import { target } from "../../src/setup.js";
import { lookAt } from "../../src/view.js";

describe("UPGRADE – deformed buildings with upgrades under the game's own tools", () => {
  useInputCity();

  it("UPGRADE-006 hovering and clicking deformed buildings and their upgrades outside the mode doesn't crash", async (context) => {
    const carPark = await building(context, "carPark");
    const school = await building(context, "school");
    const log = LogWatch.fromNow();
    for (const ref of [carPark, school]) {
      await target(ref, "Cage");
      await bridge.nudge([1, 0, 0]);
      await bridge.settled();
      await bridge.stepBack();
      await bridge.stepBack();
    }
    // Logged when a variant is first created, which may have happened earlier in this game session.
    expect(LogWatch.fromStart().matching(/Copied sim data to \d+ variants \(\d+ archetypes completed\)/), "sim data copied").not.toEqual([]);

    const wing = (await bridge.info(school)).upgrades.find((u) => /Classroom|Extension|Wing/i.test(u.prefab));
    const wingRef = wing ? (await bridge.building(wing.id)).ref : undefined;
    const storeyTop = (box: { min: number[]; max: number[] }) =>
      [(box.min[0] + box.max[0]) / 2, box.max[1] - 1.5, (box.min[2] + box.max[2]) / 2] as [number, number, number];

    await bridge.mode(false);
    try {
      for (const speed of [0, 1]) {
        await bridge.speed(speed);
        await lookAt(carPark, "front", 130);
        for (const point of [await centreOf(carPark), await pointOf(carPark, storeyTop)]) {
          await hover(point);
          await click(point);
          await bridge.frames(20);
          expect((await bridge.state()).result.inGame, `still running (speed ${speed})`).toBe(true);
        }
        await lookAt(school, "top", 160);
        for (const ref of wingRef ? [school, wingRef] : [school]) {
          const point = await centreOf(ref);
          await hover(point);
          await click(point);
          await bridge.frames(20);
          expect((await bridge.state()).result.inGame, `still running (speed ${speed})`).toBe(true);
        }
        await bridge.infoPanel();
      }
    } finally {
      await bridge.speed(0);
      await bridge.infoPanel();
      await bridge.mode(true);
    }
    expect(log.matching(/Exception/), "no exceptions in the mod's log").toEqual([]);
  }, 300_000);
});
