import { describe, expect, it } from "vitest";
import { bridge } from "../src/bridge.js";
import { corner } from "../src/expect.js";
import { building } from "../src/fixture.js";
import { move, target, useCity } from "../src/setup.js";

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
    expect(areasAfter.map((a) => a.local)).toEqual(areasBefore.map((a) => a.local));
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
});
