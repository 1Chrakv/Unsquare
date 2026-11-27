import { describe, expect, it } from "vitest";
import { bridge } from "../src/bridge.js";
import { corner, expectSamePlan } from "../src/expect.js";
import { building } from "../src/fixture.js";
import { move, target, useCity } from "../src/setup.js";
import { pack } from "../src/review.js";
import { lookAt, shot } from "../src/view.js";
import { LogWatch } from "../src/log.js";

describe("BLEND – ground blending", () => {
  useCity();

  it("BLEND-001 without blending a Building-mode wall move leaves the ground items in place", async (context) => {
    const house = await building(context, "gardenHouse");
    await target(house);
    const areasBefore = await bridge.areas(house);
    await bridge.editMode("Building");
    expect((await bridge.session()).cage!.blendMode).toBe("None");
    await move("Corner", corner(1, 0, 1), [-1.5, 0, 0]);
    await move("Corner", corner(1, 1, 1), [-1.5, 0, 0]);
    const areasAfter = await bridge.areas(house);
    expectSamePlan(areasAfter, areasBefore);
  });

  it("BLEND-003 blending is undoable", async (context) => {
    await target(await building(context, "plainHouse"));
    await bridge.editMode("Building");
    await move("Corner", corner(1, 0, 1), [-1, 0, 0]);
    await bridge.blend("All");
    expect((await bridge.settled()).blendMode).toBe("All");
    await bridge.undo();
    expect((await bridge.settled()).blendMode).toBe("None");
  });

  it("BLEND-002 with blend All the garden stays flush with the moved wall and fades out (with a ground review)", async (context) => {
    const house = await building(context, "gardenHouse");
    await target(house);
    await bridge.editMode("Building");
    await bridge.blend("All");
    await lookAt(house, "top");
    const before = await shot("BLEND-002-before", { crop: "target", ui: false });
    const log = LogWatch.fromNow();
    await move("Face", 1, [1.5, 0, 0]);
    const lot = (await bridge.session()).cage!.lotBox;
    for (const area of await bridge.areas(house))
      for (const p of area.local) {
        expect(p[0], "area stays in the lot (x)").toBeGreaterThanOrEqual(lot.min[0] - 0.3);
        expect(p[0]).toBeLessThanOrEqual(lot.max[0] + 0.3);
        expect(p[2], "area stays in the lot (z)").toBeGreaterThanOrEqual(lot.min[2] - 0.3);
        expect(p[2]).toBeLessThanOrEqual(lot.max[2] + 0.3);
      }
    expect(log.matching(/could not be remapped/)).toEqual([]);
    await bridge.clearHandle();
    await pack("BLEND-002", {
      pack: "ground",
      shots: { before, after: await shot("BLEND-002-after", { crop: "target", ui: false }) },
      log,
      focus: "The left wall (+X) moved 1.5 m out: the garden next to it stays flush against the wall and the change fades towards the lot edge; nothing bends into arcs.",
    });
  });
});
