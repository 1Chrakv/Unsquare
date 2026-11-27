import { describe, expect, it } from "vitest";
import { bridge, type HandleKind } from "../src/bridge.js";
import { building } from "../src/fixture.js";
import { LogWatch } from "../src/log.js";
import { target, useCity } from "../src/setup.js";

describe("PICK – pick modes", () => {
  useCity();

  it("PICK-001 the pick mode switches and is logged", async (context) => {
    await target(await building(context, "plainHouse"));
    for (const kind of ["Corner", "Face", "Cage", "Edge"] as HandleKind[]) {
      const log = LogWatch.fromNow();
      await bridge.pick(kind);
      expect((await bridge.session()).pickKind).toBe(kind);
      await log.waitFor(new RegExp(`\\[Edit\\] Pick mode ${kind}`));
    }
  });

  it("PICK-002 next/previous handle visit every corner, edge and face", async (context) => {
    await target(await building(context, "plainHouse"));
    for (const [kind, count] of [["Corner", 8], ["Edge", 12], ["Face", 6]] as [HandleKind, number][]) {
      await bridge.pick(kind);
      const seen = new Set<string>();
      for (let i = 0; i < count; i++) {
        await bridge.nextHandle();
        seen.add((await bridge.session()).handle!);
      }
      expect(seen.size, `${kind}: ${[...seen].join(" ")}`).toBe(count);
      const last = (await bridge.session()).handle;
      await bridge.nextHandle();
      await bridge.previousHandle();
      expect((await bridge.session()).handle).toBe(last);
      await bridge.clearHandle();
    }
  });
});
