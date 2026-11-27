import { describe, expect, it } from "vitest";
import { bridge, type Vec3 } from "../src/bridge.js";
import { corner, Face } from "../src/expect.js";
import { building, loadFixture } from "../src/fixture.js";
import { LogWatch } from "../src/log.js";
import { move, target, twoOfAKind, useCity } from "../src/setup.js";

const RunSave = "UnsquareTest-Run-Save";

/** Saves the city under the test name and loads it again; the fixture is reloaded by the next spec file. */
async function saveAndReload() {
  await bridge.stepBack();
  await bridge.stepBack();
  await bridge.save(RunSave);
  await bridge.load(RunSave);
  await bridge.mode(true);
}

const flatten = (points: Vec3[][]) => points.flat().map((p) => p.map((v) => Math.round(v * 20) / 20).join(","));

describe("SAVE – saving and loading", () => {
  useCity();

  it("SAVE-002 linked, split-with-blend and flat-terrain shapes survive save and load", async (context) => {
    const house = await building(context, "plainHouse");
    const pair = await twoOfAKind();
    if (!pair) return context.skip();
    const shapes = new Map<string, string>();

    await target(pair[0].ref);
    await move("Corner", corner(1, 1, 1), [1, 1, 0]); // linked
    shapes.set(pair[0].ref, (await bridge.session()).cage!.targetSpec);
    await bridge.stepBack();
    await bridge.stepBack();

    await target(pair[1].ref);
    await bridge.editMode("Building");
    await move("Face", Face.top, [1, 0, 0]); // split
    await bridge.blend("All");
    shapes.set(pair[1].ref, (await bridge.settled()).targetSpec);
    await bridge.editMode("Both");
    await bridge.stepBack();
    await bridge.stepBack();

    await target(house);
    await move("Face", Face.bottom, [0, 2, 0]);
    await bridge.toggleTerrain(); // flat terrain
    shapes.set(house, (await bridge.settled()).targetSpec);

    const log = LogWatch.fromNow();
    await saveAndReload();
    await log.waitFor(/Save: \d+ deformed buildings as base \+ cage/);
    await log.waitFor(/Save: \d+ buildings back on their variants/);
    const restored = await log.waitFor(/Saved cages: \d+ buildings on \d+ variants .*, 0 failed/, 60_000);
    expect(restored).toBeTruthy();
    expect(log.matching(/Unknown prefab ID.*__uq/)).toHaveLength(0);
    for (const [ref, spec] of shapes) expect((await bridge.settled(ref)).targetSpec, ref).toBe(spec);
    await loadFixture({ fresh: true });
  });

  it("SAVE-003 five save and load cycles move no path, area or lot prop", async (context) => {
    const house = await building(context, "gardenHouse");
    await target(house);
    await move("Corner", corner(1, 0, 1), [1.5, 0, 0]);
    await move("Corner", corner(0, 0, 1), [-1, 0, 0]);
    const snapshot = async () => ({
      // Area entities come back in another order after a load; each area keeps its points in order.
      areas: (await bridge.areas(house)).map((a) => flatten([a.local]).join(" ")).sort(),
      nets: flatten([(await bridge.nets(house)).nodes.map((n) => n.local)]).sort(),
      props: flatten([(await bridge.subObjects(house)).map((s) => s.local)]).sort(),
    });
    const first = await snapshot();
    for (let i = 0; i < 5; i++) {
      await saveAndReload();
      await bridge.settled(house);
      expect(await snapshot(), `after cycle ${i + 1}`).toEqual(first);
    }
    await loadFixture({ fresh: true });
  });
});
