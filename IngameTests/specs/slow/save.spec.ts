import fs from "node:fs";
import path from "node:path";
import { afterAll, beforeAll, describe, expect, it } from "vitest";
import { bridge } from "../../src/bridge.js";
import { corner, Face } from "../../src/expect.js";
import { building, cleanUp, loadFixture } from "../../src/fixture.js";
import { game, ModDataBackup, waitUntil } from "../../src/game.js";
import { LogWatch } from "../../src/log.js";
import { deployedMod, playerLog } from "../../src/paths.js";
import { move, target } from "../../src/setup.js";

const RunSave = "UQTest-Run-Slow";

/** Deforms the plain house, saves the city under the run name and returns the shape. */
async function deformAndSave(context: Parameters<typeof building>[0]) {
  const house = await building(context, "plainHouse");
  await target(house);
  await move("Corner", corner(1, 1, 1), [1.5, 1, 0]);
  await move("Face", Face.bottom, [0, 1, 0]);
  const spec = (await bridge.settled()).targetSpec;
  await bridge.stepBack();
  await bridge.save(RunSave);
  return { house, spec };
}

describe("SAVE – saving and loading across restarts", () => {
  let backup: ModDataBackup;
  beforeAll(async () => {
    await loadFixture({ fresh: true });
    await cleanUp();
  });
  afterAll(async () => {
    await game.ensureRunning();
  });

  it("SAVE-001 deformed buildings survive reload and a restart", async (context) => {
    const { house, spec } = await deformAndSave(context);
    await bridge.load(RunSave);
    expect((await bridge.settled(house)).targetSpec).toBe(spec);
    await game.restart();
    const log = LogWatch.fromStart();
    await bridge.load(RunSave);
    expect((await bridge.settled(house)).targetSpec).toBe(spec);
    await log.waitFor(/Saved cages: \d+ buildings on \d+ variants .*, 0 failed/);
  });

  it("SAVE-007 without the manifest files a new-format save loads its shapes from the save", async (context) => {
    const { house, spec } = await deformAndSave(context);
    await game.quit();
    backup = ModDataBackup.create("save-007");
    backup.moveAside("variants.json", "variants.json.bak", "variants.archive.json", "variants.archive.json.bak");
    try {
      await game.start();
      const log = LogWatch.fromStart();
      await bridge.load(RunSave);
      expect((await bridge.settled(house)).targetSpec).toBe(spec);
      await log.waitFor(/Restored 0 variants/);
      await log.waitFor(/Saved cages: .*, 0 failed/);
      expect(log.matching(/has no instance archetype|Not swapping to/)).toHaveLength(0);
    } finally {
      await game.quit();
      backup.restore();
      await game.start();
    }
  });

  it("SAVE-004 without Unsquare a save loads with plain buildings and no errors", async (context) => {
    await deformAndSave(context);
    await game.quit();
    const aside = `${deployedMod}.disabled-by-tests`;
    fs.renameSync(deployedMod, aside);
    try {
      // Without the mod there is no bridge: the game continues the last save (ours) and the logs tell the rest.
      const started = Date.now();
      game.launch(["-continuelastsave"]);
      await waitUntil(() => sceneLog(started).filter((l) => /Loading completed/.test(l)).length >= 2, 600_000, "the save to load");
      await new Promise((resolve) => setTimeout(resolve, 20_000));
      const player = fs.readFileSync(playerLog, "utf8");
      expect(player).toMatch(/Not serializable type .*SavedCage/);
      expect(player).not.toMatch(/Exception[^\n]*\n[^\n]*Unsquare/);
    } finally {
      await game.kill();
      if (fs.existsSync(aside)) fs.renameSync(aside, deployedMod);
    }
  });
});

/** Lines of SceneFlow.log written after `since` (ms since epoch). */
function sceneLog(since: number): string[] {
  const file = path.join(path.dirname(playerLog), "Logs", "SceneFlow.log");
  if (!fs.existsSync(file) || fs.statSync(file).mtimeMs < since) return [];
  return fs.readFileSync(file, "utf8").split(/\r?\n/);
}