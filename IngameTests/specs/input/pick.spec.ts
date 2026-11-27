import { describe, expect, it } from "vitest";
import { bridge } from "../../src/bridge.js";
import { building } from "../../src/fixture.js";
import { centreOf, click, hover, useInputCity } from "../../src/input.js";
import { LogWatch } from "../../src/log.js";
import { lookAt } from "../../src/view.js";

/** The pixel position of a handle of the target, nudged by a few pixels so the pick has to search. */
async function near(handle: string, dx = 4, dy = 3) {
  const point = await bridge.screenPoint({ handle });
  return { x: point.x! + dx, y: point.y! + dy };
}

describe("PICK – picking handles with the mouse", () => {
  useInputCity();

  it("PICK-003 hovering highlights the nearest handle of the pick mode", async (context) => {
    const house = await building(context, "plainHouse");
    await bridge.select(house);
    await lookAt(house, "front", 110);
    for (const [kind, handle] of [["Corner", "Corner7"], ["Corner", "Corner6"], ["Face", "Face5"], ["Edge", "Edge3"]] as const) {
      await bridge.pick(kind);
      expect((await hover(await near(handle))).handle, `${kind}: the mouse near ${handle}`).toBe(handle);
    }
  });

  it("PICK-004 in cage mode hovering inside highlights the cage; a click selects it with the gizmo at the centre", async (context) => {
    const house = await building(context, "plainHouse");
    await bridge.select(house);
    await bridge.pick("Cage");
    await lookAt(house, "front", 110);
    expect((await hover(await centreOf(house))).handle).toBe("Cage0");
    const log = LogWatch.fromNow();
    await click(await centreOf(house));
    expect((await bridge.session()).handle).toBe("Cage0");
    await log.waitFor(/\[Edit\] Selected Cage0/);
    const centre = await bridge.screenPoint({ handle: "Cage0" });
    const gizmo = (await bridge.screenPoint({ gizmo: "arrowY" })).gizmo!;
    for (let i = 0; i < 3; i++) expect(gizmo.origin[i], "gizmo at the cage centre").toBeCloseTo(centre.world[i], 1);
  });
});
