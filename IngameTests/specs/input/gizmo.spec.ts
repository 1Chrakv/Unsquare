import { describe, expect, it } from "vitest";
import { bridge, type Vec3 } from "../../src/bridge.js";
import { Face } from "../../src/expect.js";
import { building } from "../../src/fixture.js";
import { press, tap, useInputCity } from "../../src/input.js";
import { LogWatch } from "../../src/log.js";
import { target } from "../../src/setup.js";
import { button, clickButton } from "../../src/ui.js";
import { lookAt } from "../../src/view.js";


/** The gizmo length across the view as a share of the screen height, and the arrow's length in metres. */
async function gizmoOnScreen(cameraPosition: Vec3) {
  const arrow = await bridge.screenPoint({ gizmo: "arrowY" });
  const o = arrow.gizmo!.origin;
  const view = [o[0] - cameraPosition[0], o[2] - cameraPosition[2]];
  const n = Math.hypot(view[0], view[1]);
  const length = arrow.gizmo!.length;
  const a = await bridge.screenPoint({ world: o });
  const b = await bridge.screenPoint({ world: [o[0] - (view[1] / n) * length, o[1], o[2] + (view[0] / n) * length] });
  return { share: Math.hypot(b.x! - a.x!, b.y! - a.y!) / a.screen.height, length };
}

/** Drags the Y arrow 60 px up the screen and says whether the top moved; undoes it. */
async function grabsArrow() {
  const before = (await bridge.session()).cage!.targetOffsets;
  const grab = await bridge.screenPoint({ gizmo: "arrowY" });
  await bridge.mouse("drag", { at: { gizmo: "arrowY" }, to: { x: grab.x!, y: grab.y! - 60 }, frames: 6, settleFrames: 3 });
  const after = (await bridge.settled()).targetOffsets;
  if (after !== before) await bridge.undo();
  await bridge.settled();
  return after !== before;
}

describe("GIZMO – the gizmo with the mouse", () => {
  useInputCity();

  it("GIZMO-002 the gizmo keeps its screen size when zooming, is never shorter than the minimum and stays grabbable", async (context) => {
    const house = await building(context, "plainHouse");
    await target(house, "Face", Face.top);
    const shares: number[] = [];
    for (const distance of [80, 250]) {
      const camera = await lookAt(house, "front", distance);
      shares.push((await gizmoOnScreen(camera.position)).share);
      expect(await grabsArrow(), `arrow grabbable at ${distance} m`).toBe(true);
    }
    expect(Math.abs(shares[0] - shares[1]) / shares[0], `same screen size: ${shares.join(" / ")}`).toBeLessThan(0.05);
    // Close up the minimum length takes over. The camera pivot follows the terrain, so use the bottom face's gizmo.
    await bridge.handle("Face", Face.bottom);
    const close = await lookAt(house, "front", 25);
    const { length } = await gizmoOnScreen(close.position);
    expect(length, "never shorter than the minimum gizmo length").toBeGreaterThanOrEqual((await bridge.overlay()).axisLength - 1e-3);
    expect(await grabsArrow(), "arrow grabbable close up").toBe(true);
  });

  it("GIZMO-003 the toggle and a Ctrl tap switch Move/Rotate; not during a drag, Ctrl+Z or Ctrl+Alt+arrows", async (context) => {
    const house = await building(context, "plainHouse");
    await target(house, "Face", Face.top);
    await lookAt(house, "front", 110);
    const gizmo = async () => (await bridge.session()).gizmo;

    const log = LogWatch.fromNow();
    await clickButton("gizmo-rotate");
    expect(await gizmo(), "the tool-window toggle switches to Rotate").toBe("Rotate");
    await log.waitFor(/\[Edit\] Gizmo mode Rotate/);
    expect((await button("gizmo-rotate"))?.active, "the toggle shows the session's mode").toBe(true);
    await clickButton("gizmo-move");
    expect(await gizmo()).toBe("Move");

    // During a drag a tap changes nothing: the drag keeps the part it grabbed.
    const grab = await bridge.screenPoint({ gizmo: "arrowY" });
    await bridge.mouse("drag", { at: { gizmo: "arrowY" }, to: { x: grab.x!, y: grab.y! - 40 }, frames: 4, hold: true });
    await tap("LeftCtrl");
    expect(await gizmo(), "no switch during a drag").toBe("Move");
    await bridge.mouse("release");
    await bridge.settled();

    await tap("LeftCtrl", "Z");
    expect(await gizmo(), "Ctrl+Z doesn't switch").toBe("Move");
    await press("UnsquareAlignGroundLeft");
    expect(await gizmo(), "Ctrl+Alt+arrow doesn't switch").toBe("Move");

    await tap("LeftCtrl");
    expect(await gizmo(), "a Ctrl tap switches to Rotate (issue 010)").toBe("Rotate");
    expect((await button("gizmo-rotate"))?.active, "and the toggle follows").toBe(true);
  });
});

