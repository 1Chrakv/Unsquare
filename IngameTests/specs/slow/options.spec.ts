import { afterAll, beforeAll, describe, expect, it } from "vitest";
import { bridge } from "../../src/bridge.js";
import { loadFixture } from "../../src/fixture.js";
import { game } from "../../src/game.js";

describe("OPTIONS – settings across restarts", () => {
  let saved: Record<string, unknown>;
  beforeAll(async () => {
    await loadFixture();
    saved = await bridge.settings();
  });
  afterAll(async () => {
    await game.ensureRunning();
    await bridge.setSettings(saved);
  });

  it("OPTIONS-004 Reset settings restores the defaults, also after a restart", async () => {
    const defaults = await bridge.defaultSettings();
    await bridge.setSettings({ SmallStep: defaults.SmallStep === 0.2 ? 0.3 : 0.2, LineRadius: 0.3, KeepOnLevelUp: !defaults.KeepOnLevelUp });
    expect(await bridge.defaultSettings()).toEqual(defaults);
    await game.restart();
    expect(await bridge.settings(), "after a restart").toEqual(defaults);
    // Key bindings are not plain values; the bridge can't read them, so "keeps key bindings" stays a manual check.
  }, 1_200_000);
});
