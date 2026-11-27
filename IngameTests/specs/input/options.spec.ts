import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { bridge } from "../../src/bridge.js";
import { Face } from "../../src/expect.js";
import { building } from "../../src/fixture.js";
import { click, groundBefore, hover, useInputCity } from "../../src/input.js";
import { target } from "../../src/setup.js";
import { lookAt } from "../../src/view.js";

describe("OPTIONS – edit options with real input", () => {
  useInputCity();
  let saved: Record<string, unknown>;
  beforeEach(async () => {
    saved = await bridge.settings();
  });
  afterEach(async () => {
    await bridge.setSettings(saved);
  });

  it("OPTIONS-005 pick radius, far-click margin and gizmo size take effect at once", async (context) => {
    const house = await building(context, "plainHouse");
    await bridge.select(house);
    await bridge.pick("Corner");
    await lookAt(house, "front", 110);

    // 25 px beside the corner: outside a 12 px pick radius, inside a 40 px one.
    const corner = await bridge.screenPoint({ handle: "Corner7" });
    const beside = { x: corner.x! + 25, y: corner.y! };
    await bridge.setSettings({ PickRadius: 12 });
    expect((await hover(beside)).handle, "12 px: too far").not.toBe("Corner7");
    await bridge.setSettings({ PickRadius: 40 });
    expect((await hover(beside)).handle, "40 px: picked").toBe("Corner7");

    // Ground 6 m before the lot: outside a 5 % margin of the 32 m cage, inside a 50 % one.
    await target(house, "Face", Face.top);
    await bridge.setSettings({ FarClickMargin: 50 });
    await click(await groundBefore(house, 6));
    expect((await bridge.session()).handle, "50 %: near, the selection stays").toBe("Face3");
    await bridge.setSettings({ FarClickMargin: 5 });
    await click(await groundBefore(house, 6));
    expect((await bridge.session()).handle, "5 %: far, the click steps back").toBeNull();

    await bridge.handle("Face", Face.top);
    await bridge.setSettings({ GizmoSize: 6 });
    const small = (await bridge.screenPoint({ gizmo: "arrowY" })).gizmo!.length;
    await bridge.setSettings({ GizmoSize: 12 });
    const large = (await bridge.screenPoint({ gizmo: "arrowY" })).gizmo!.length;
    expect(large / small, "double gizmo size, double length").toBeCloseTo(2, 1);
  });

  it("OPTIONS-006 at a 20 px drag threshold small jitter on a press doesn't drag", async (context) => {
    const house = await building(context, "plainHouse");
    await bridge.setSettings({ DragThreshold: 20 });
    await bridge.select(house);
    await bridge.pick("Corner");
    await lookAt(house, "front", 110);
    const corner = await bridge.screenPoint({ handle: "Corner7" });
    await bridge.mouse("drag", { at: { handle: "Corner7" }, to: { x: corner.x! + 10, y: corner.y! + 4 }, frames: 5, settleFrames: 3 });
    let session = await bridge.session();
    expect(session.handle, "the press selected the handle").toBe("Corner7");
    expect(session.cage!.targetOffsets, "10 px of jitter moved nothing").toBe(session.cage!.appliedOffsets);
    expect(session.canUndo, "nothing to undo").toBe(false);
    await bridge.mouse("drag", { at: { handle: "Corner7" }, to: { x: corner.x! + 60, y: corner.y! }, frames: 8, settleFrames: 3 });
    session = await bridge.session();
    expect(session.canUndo, "60 px drags").toBe(true);
  });
});
