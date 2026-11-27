import { afterAll, beforeAll, describe, expect, it } from "vitest";
import { bridge } from "../../src/bridge.js";
import { corner, Face } from "../../src/expect.js";
import { building, cleanUp, loadFixture } from "../../src/fixture.js";
import { game } from "../../src/game.js";
import { LogWatch } from "../../src/log.js";
import { bakes, move, target } from "../../src/setup.js";

const RunSave = "UnsquareTest-Run-Restart";
const Timeout = 1_200_000;

/** Saves the city under the run name (no target, so nothing is mid-edit). */
async function save() {
  for (let i = 0; i < 4 && (await bridge.session()).target; i++) await bridge.stepBack();
  await bridge.save(RunSave);
}

/** Loads the run save, in a fresh game when `restart`; returns a log watch started before the load. */
async function load(restart = false) {
  if (restart) await game.restart();
  const log = restart ? LogWatch.fromStart() : LogWatch.fromNow();
  await bridge.load(RunSave);
  await bridge.speed(0);
  await bridge.mode(true);
  return log;
}

/** Terrain heights on the lot's centre line (20–80 % from back to front), relative to the building's ground. */
async function centreLine(ref: string) {
  const { min, max } = (await bridge.settled(ref)).lotBox;
  const x = (min[0] + max[0]) / 2;
  const points = [0.2, 0.35, 0.5, 0.65, 0.8].map((f) => [x, min[2] + (max[2] - min[2]) * f] as [number, number]);
  return bridge.heights(points, ref);
}

describe("Shapes and the edit mode across saves and restarts", () => {
  beforeAll(async () => {
    await loadFixture({ fresh: true });
    await cleanUp();
  });
  afterAll(async () => {
    await game.ensureRunning();
  });

  it("TERRAIN-010 a lot lifted by more than 5 m looks the same after load, save + load and restart + load", async (context) => {
    const house = await building(context, "plainHouse");
    await target(house);
    const lifted = LogWatch.fromNow();
    await move("Face", Face.bottom, [0, 6, 0]);
    await bridge.frames(30);
    const pad = /pad ([-\d.]+) m/.exec(await lifted.waitFor(/Terrain slope for prefab .*: pad/))![1];
    expect(Number(pad), "pad lift").toBeGreaterThan(5);
    const heights = await centreLine(house);
    await save();

    for (const restart of [false, true]) {
      const log = await load(restart);
      await bridge.settled(house);
      await bridge.frames(30);
      const line = await log.waitFor(/Terrain slope for prefab .*: pad/, 60_000);
      expect(line, restart ? "after a restart" : "after a load").toContain(`pad ${pad} m`);
      (await centreLine(house)).forEach((h, i) => expect(Math.abs(h - heights[i]), `height ${i}`).toBeLessThan(0.3));
    }
  }, Timeout);

  it("SPLIT-010 a lot-only deformation survives save, load and restart without a rebake", async (context) => {
    await loadFixture({ fresh: true });
    const house = await building(context, "plainHouse");
    await target(house);
    await bridge.editMode("Lot");
    await move("Corner", corner(1, 0, 1), [1.5, 0, 0]);
    const spec = (await bridge.settled()).targetSpec;
    await bridge.editMode("Both");
    await save();

    for (const restart of [false, true]) {
      const log = await load(restart);
      expect((await bridge.settled(house)).targetSpec, restart ? "after a restart" : "after a load").toBe(spec);
      const restored = await log.waitFor(/Saved cages: \d+ buildings on \d+ variants/, 60_000);
      expect(restored, "the shape comes from cached assets").toMatch(/\(0 new assets/);
      expect(bakes(log).filter((b) => b.newAssets > 0)).toHaveLength(0);
    }
  }, Timeout);

  it("SPLIT-012 the last chosen edit mode is active again after a restart", async (context) => {
    await loadFixture();
    await target(await building(context, "plainHouse"));
    await bridge.editMode("Building");
    try {
      expect((await bridge.settings()).EditMode).toBe("Building");
      await game.restart();
      await loadFixture();
      await target(await building(context, "plainHouse"));
      expect((await bridge.session()).editMode).toBe("Building");
    } finally {
      await bridge.editMode("Both");
    }
  }, Timeout);

  it("DRAG-014 a save during a drag keeps the drag going and holds the shape of that moment", async (context) => {
    await loadFixture({ fresh: true });
    const house = await building(context, "plainHouse");
    await target(house, "Corner", corner(1, 1, 1));
    await bridge.drag({ translate: [2, 0, 0], frames: 3, hold: true });
    await bridge.frames(90); // the paused drag bakes after the rebake delay
    const during = (await bridge.session()).cage!;
    await bridge.save(RunSave);
    expect((await bridge.session()).dragging, "the drag goes on").toBe(true);
    await bridge.dragEnd();
    const after = await bridge.settled();
    expect(after.targetSpec).toBe(during.targetSpec);
    await bridge.stepBack();
    await bridge.stepBack();
    await load();
    expect((await bridge.settled(house)).targetSpec, "the saved shape").toBe(during.appliedSpec);
  }, Timeout);
});
