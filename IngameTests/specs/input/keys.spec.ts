import { describe, expect, it } from "vitest";
import { bridge } from "../../src/bridge.js";
import { Face } from "../../src/expect.js";
import { building } from "../../src/fixture.js";
import { press, useInputCity } from "../../src/input.js";
import { LogWatch } from "../../src/log.js";
import { target } from "../../src/setup.js";

/**
 * Every hotkey of `CageHotkeys` (Unsquare/UI/Input/CageHotkeys.cs), in an order where each one has something to do
 * on the plain house with its top face selected. The mode toggle comes last.
 */
const Hotkeys = [
  "UnsquareMovePosY", "UnsquareMoveNegY", "UnsquareMovePosX", "UnsquareMoveNegX", "UnsquareMovePosZ", "UnsquareMoveNegZ",
  "UnsquareCycleStep", "UnsquareUndo", "UnsquareRedo", "UnsquareResetHandle", "UnsquareNextHandle", "UnsquarePreviousHandle",
  "UnsquareKindCorner", "UnsquareKindEdge", "UnsquareKindFace", "UnsquareKindCage", "UnsquareCyclePickKind",
  "UnsquareCycleEditMode", "UnsquareLinkBuildingToLot", "UnsquareLinkLotToBuilding", "UnsquareToggleBlendMode",
  "UnsquareToggleTerrainLift", "UnsquareFitToGround", "UnsquareToggleGroundFitMode", "UnsquareAlignLeft",
  "UnsquareAlignBack", "UnsquareAlignRight", "UnsquareAlignFront", "UnsquareCycleAlignMode", "UnsquareAlignGroundLeft",
  "UnsquareAlignGroundBack", "UnsquareAlignGroundRight", "UnsquareAlignGroundFront", "UnsquareResetSelected",
  "UnsquareResetAll", "UnsquareLogState", "UnsquareToggleMode",
];

describe("KEYS – hotkeys with the keyboard", () => {
  useInputCity();

  it("KEYS-001 every default hotkey runs its command in the mode", async (context) => {
    const house = await building(context, "plainHouse");
    await target(house, "Face", Face.top);
    const silent: string[] = [];
    const conflicts: string[] = [];
    try {
      for (const binding of Hotkeys) {
        const log = LogWatch.fromNow();
        const { keys } = await press(binding);
        // Every session command logs an [Edit] line (also when it has nothing to do).
        try {
          await log.waitFor(/\[Edit\]/, 3000);
        } catch {
          silent.push(binding);
        }
        if (binding === "UnsquareToggleMode") break;
        // Another mod's hotkey on the same keys can switch to its tool, which ends Unsquare mode: note it, go on.
        const tool = (await bridge.state()).result.tool;
        if (tool !== "Unsquare") {
          conflicts.push(`${binding} (${keys}) also opened ${tool}`);
          await bridge.tool("default");
          await bridge.mode(true);
          await target(house, "Face", Face.top);
        } else if (!(await bridge.session()).handle) await bridge.handle("Face", Face.top);
      }
      if (conflicts.length > 0) console.warn(`Hotkey conflicts with other mods: ${conflicts.join("; ")}`);
      expect(silent, "hotkeys that ran no command").toEqual([]);
    } finally {
      if (!(await bridge.session()).mode) await bridge.mode(true);
    }
  });

  it("KEYS-002 a rebound hotkey works with its new key and no longer with the old one", async (context) => {
    const house = await building(context, "plainHouse");
    await target(house, "Corner", 0);
    const rebound = await bridge.rebind("UnsquareNextHandle", "<Keyboard>/k");
    try {
      const pressed = await press("UnsquareNextHandle");
      expect(pressed.keys, "the bridge presses the new key").toBe("<Keyboard>/k");
      expect((await bridge.session()).handle, "the new key works").toBe("Corner1");
      // The old key with the binding's modifiers (Alt): nothing happens.
      await bridge.key({ keys: ["LeftAlt", rebound.previous.slice(rebound.previous.lastIndexOf("/") + 1)] });
      expect((await bridge.session()).handle, "the old key does nothing").toBe("Corner1");
    } finally {
      await bridge.rebind("UnsquareNextHandle", rebound.previous);
    }
  });
});
