import { describe, expect, it } from "vitest";
import { bridge, BridgeError, type GizmoPart, type Vec3 } from "../src/bridge.js";
import { corner, Face } from "../src/expect.js";
import { building } from "../src/fixture.js";
import { target, useCity } from "../src/setup.js";
import { lookAt } from "../src/view.js";

const sub = (a: Vec3, b: Vec3): Vec3 => [a[0] - b[0], a[1] - b[1], a[2] - b[2]];
const unit = (v: Vec3): Vec3 => {
  const length = Math.hypot(...v);
  return [v[0] / length, v[1] / length, v[2] / length];
};
const dot = (a: Vec3, b: Vec3) => a[0] * b[0] + a[1] * b[1] + a[2] * b[2];

/** The parts of the gizmo the bridge can point at; a ring the handle can't turn around answers 409. */
async function rings(): Promise<string[]> {
  const shown: string[] = [];
  for (const part of ["ringX", "ringY", "ringZ"] as GizmoPart[]) {
    try {
      await bridge.screenPoint({ gizmo: part });
      shown.push(part);
    } catch (error) {
      if (!(error instanceof BridgeError) || error.reply.status !== 409) throw error;
    }
  }
  return shown;
}

describe("GIZMO – the move and rotate gizmo", () => {
  useCity();

  it("GIZMO-001 the arrows point along the axes the move keys use", async (context) => {
    const house = await building(context, "plainHouse");
    await target(house, "Corner", corner(1, 1, 1));
    await lookAt(house, "front");
    const axes: [GizmoPart, Vec3][] = [["arrowX", [1, 0, 0]], ["arrowY", [0, 1, 0]], ["arrowZ", [0, 0, 1]]];
    for (const [part, nudge] of axes) {
      const arrow = await bridge.screenPoint({ gizmo: part });
      const direction = unit(sub(arrow.world, arrow.gizmo!.origin));
      const before = (await bridge.screenPoint({ handle: "Corner7" })).world;
      await bridge.nudge(nudge);
      const after = (await bridge.screenPoint({ handle: "Corner7" })).world;
      const moved = unit(sub(after, before));
      expect(dot(direction, moved), `${part} along the ${nudge} nudge`).toBeGreaterThan(0.99);
      await bridge.undo();
    }
  });

  it("GIZMO-004 in Rotate mode faces get three rings, edges two, corners keep the arrows", async (context) => {
    const house = await building(context, "plainHouse");
    await lookAt(house, "front");
    await bridge.gizmo("Rotate");
    try {
      await target(house, "Face", Face.top);
      expect((await bridge.session()).showsRings).toBe(true);
      expect(await rings()).toEqual(["ringX", "ringY", "ringZ"]);

      // Edges 0-3 run along X, 4-7 along Y, 8-11 along Z: no ring around their own direction.
      for (const [index, own] of [[0, "ringX"], [4, "ringY"], [8, "ringZ"]] as [number, string][]) {
        await bridge.handle("Edge", index);
        const shown = await rings();
        expect(shown, `edge ${index}`).toHaveLength(2);
        expect(shown).not.toContain(own);
      }

      await bridge.handle("Corner", 0);
      expect((await bridge.session()).showsRings, "corners keep the arrows").toBe(false);
      expect(await rings()).toEqual([]);
      await bridge.screenPoint({ gizmo: "arrowY" });
    } finally {
      await bridge.gizmo("Move");
    }
  });

  it("GIZMO-005 after Reset settings the gizmo is 6 % of the view height, at least 1 m", async (context) => {
    const saved = await bridge.settings();
    try {
      await bridge.defaultSettings();
      const overlay = await bridge.overlay();
      expect(overlay.gizmoScreenSize).toBeCloseTo(0.06, 5);
      expect(overlay.axisLength).toBeCloseTo(1, 5);

      const house = await building(context, "plainHouse");
      await target(house, "Face", Face.top);
      // Far away: the vertical arrow covers about 6 % of the screen height (less its foreshortening at 30° pitch).
      await lookAt(house, "front", 250);
      const arrow = await bridge.screenPoint({ gizmo: "arrowY" });
      const origin = await bridge.screenPoint({ world: arrow.gizmo!.origin });
      const tip = await bridge.screenPoint({ world: [origin.world[0], origin.world[1] + arrow.gizmo!.length, origin.world[2]] });
      const fraction = Math.abs(origin.y! - tip.y!) / origin.screen.height;
      expect(fraction, "arrow height on screen").toBeGreaterThan(0.045);
      expect(fraction).toBeLessThan(0.065);

      // Close up: never shorter than 1 m.
      await lookAt(house, "front", 12);
      expect((await bridge.screenPoint({ gizmo: "arrowY" })).gizmo!.length).toBeGreaterThanOrEqual(1 - 1e-3);
    } finally {
      await bridge.setSettings(saved);
    }
  });
});
