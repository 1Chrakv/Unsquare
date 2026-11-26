import fs from "node:fs";
import path from "node:path";
import { afterAll, beforeAll, describe, expect, it } from "vitest";
import { bridge } from "../../src/bridge.js";
import { corner } from "../../src/expect.js";
import { building, cleanUp, loadFixture, type TestContext } from "../../src/fixture.js";
import { game, ModDataBackup } from "../../src/game.js";
import { LogWatch } from "../../src/log.js";
import { modData } from "../../src/paths.js";
import { bakes, move, target } from "../../src/setup.js";

const RunSave = "UnsquareTest-Run-Cache";
const Timeout = 1_500_000;

/**
 * Deforms the plain house into a shape whose assets are new (baked now), saves the city and returns the asset
 * files the bake wrote.
 */
async function freshShapeSaved(context: TestContext) {
  const house = await building(context, "plainHouse");
  await target(house, "Corner", corner(1, 1, 1));
  const before = new Set(ModDataBackup.assetFiles());
  for (let attempt = 0; attempt < 10; attempt++) {
    const log = LogWatch.fromNow();
    await bridge.nudge([1, 0, 0]);
    const spec = (await bridge.settled()).targetSpec;
    await log.waitFor(/Bake for building/, 20_000);
    if (bakes(log).some((b) => b.newAssets > 0)) {
      const files = ModDataBackup.assetFiles().filter((name) => !before.has(name));
      for (let i = 0; i < 4 && (await bridge.session()).target; i++) await bridge.stepBack();
      await bridge.save(RunSave);
      return { house, spec, files };
    }
  }
  throw new Error("Ten nudges found no shape that wasn't cached already.");
}

describe("CACHE – the geometry asset cache", () => {
  let backup: ModDataBackup;
  beforeAll(async () => {
    await loadFixture({ fresh: true });
    await cleanUp();
    backup = ModDataBackup.create("cache");
    backup.copyAssets();
  });
  afterAll(async () => {
    await game.quit();
    backup.restore();
    backup.restoreAssets();
    await game.start();
  });

  it("CACHE-002 a save whose shapes were evicted rebakes them on load, also on a second load", async (context) => {
    const { house, spec, files } = await freshShapeSaved(context);
    expect(files.length, "the bake wrote assets").toBeGreaterThan(0);
    await game.quit();
    const aside = ModDataBackup.create("cache-002");
    aside.moveAside(...files); // as housekeeping would have evicted them
    await game.start();
    for (const pass of ["first", "second"]) {
      const log = LogWatch.fromNow();
      await bridge.load(RunSave);
      expect((await bridge.settled(house)).targetSpec, `${pass} load`).toBe(spec);
      const restored = await log.waitFor(/Saved cages: \d+ buildings on \d+ variants/, 60_000);
      if (pass === "first") expect(restored, "rebaked").toMatch(/\(([1-9]\d*) new assets/);
      expect(restored).toMatch(/, 0 failed/);
    }
  }, Timeout);

  it("BAKE-007 the source meshes are released after a load that rebaked and when leaving the city", async (context) => {
    const { files } = await freshShapeSaved(context);
    await game.quit();
    ModDataBackup.create("bake-007").moveAside(...files);
    await game.start();
    const log = LogWatch.fromNow();
    await bridge.load(RunSave);
    await log.waitFor(/Saved cages: .*\(([1-9]\d*) new assets/, 60_000);
    await log.waitFor(/Released source meshes of \d+ render prefabs/, 60_000);
    const leaving = LogWatch.fromNow();
    await target((await bridge.find({ variant: true, limit: 1 }))[0].ref, "Corner", corner(1, 1, 1));
    await bridge.nudge([0, 1, 0]); // holds sources again
    await bridge.settled();
    await bridge.stepBack();
    await bridge.stepBack();
    await bridge.mainMenu();
    await leaving.waitFor(/Released source meshes of \d+ render prefabs/, 60_000);
  }, Timeout);

  it("CACHE-001 with a 0.5 GB cache, housekeeping at start evicts down to the cap", async () => {
    await game.ensureRunning();
    const cap = (await bridge.settings()).GeometryCacheGigabytes;
    await bridge.setSettings({ GeometryCacheGigabytes: 0.5 });
    try {
      await game.restart();
      // Housekeeping runs at the first preload, i.e. when a city is loaded.
      await loadFixture({ fresh: true });
      const line = await LogWatch.fromStart().waitFor(/Housekeeping: .*evicted \d+ assets/, 60_000);
      const [, evicted, cache, max] = /evicted (\d+) assets .*cache (\d+) of (\d+) MB/.exec(line)!.map(Number);
      const claimed = Number(/legacy-claimed (\d+) MB/.exec(line)?.[1] ?? 0);
      expect(max, "0.5 GB").toBeGreaterThan(470);
      expect(max).toBeLessThan(530);
      expect(cache, line).toBeLessThanOrEqual(max);
      const onDisk = ModDataBackup.assetFiles().reduce((sum, name) => sum + fs.statSync(path.join(modData, name)).size, 0);
      expect(onDisk / 2 ** 20, `${evicted} evicted; on disk: the cap plus legacy-claimed assets`).toBeLessThan(max + claimed + 64);
    } finally {
      await bridge.setSettings({ GeometryCacheGigabytes: cap });
    }
  }, Timeout);
});
