import { afterEach, describe, expect, it } from "vitest";
import { bridge } from "../src/bridge.js";
import { LogWatch } from "../src/log.js";
import { useCity } from "../src/setup.js";

describe("OPTIONS – settings", () => {
  useCity();
  let saved: Record<string, unknown> | undefined;

  /** Changes settings for one test; afterEach puts the previous values back. */
  async function change(values: Record<string, unknown>) {
    saved ??= await bridge.settings();
    return bridge.setSettings(values);
  }

  afterEach(async () => {
    if (saved) await bridge.setSettings(saved);
    saved = undefined;
  });

  it("OPTIONS-002 every settings change logs Options once", async () => {
    const current = (await bridge.settings()).SmallStep as number;
    const log = LogWatch.fromNow();
    await change({ SmallStep: current === 0.2 ? 0.3 : 0.2 });
    await log.waitFor(/Options: steps /);
    expect(log.matching(/Options: /)).toHaveLength(1);
    await bridge.setSettings({ SmallStep: (await bridge.settings()).SmallStep });
    expect(log.matching(/Options: /), "an unchanged apply logs nothing").toHaveLength(1);
  });
});
