import { describe, expect, it } from "vitest";
import { bridge, type GizmoPart, type Vec3 } from "../../src/bridge.js";
import { corner, Face, offsets } from "../../src/expect.js";
import { building } from "../../src/fixture.js";
import { useInputCity } from "../../src/input.js";
import { target } from "../../src/setup.js";
import { lookAt, type Preset } from "../../src/view.js";

const Top = [2, 3, 6, 7];
const allZero = (text: string) => offsets(text).every((v) => v.every((c) => c === 0));

/**
 * Presses on a gizmo part and moves the mouse in `steps` screen steps of (dx, dy) pixels, reading the requested shape
 * after each; releases at the end. Returns the offsets after every step.
 */
async function sampledDrag(part: GizmoPart, dx: number, dy: number, steps: number): Promise<Vec3[][]> {
  const grab = await bridge.screenPoint({ gizmo: part });
  await bridge.mouse("drag", { at: { gizmo: part }, to: { x: grab.x!, y: grab.y! }, frames: 1, hold: true });
  const samples: Vec3[][] = [];
  for (let i = 1; i <= steps; i++) {
    await bridge.mouse("move", { at: { x: grab.x! + dx * i, y: grab.y! + dy * i }, settleFrames: 1 });
    samples.push(offsets((await bridge.session()).cage!.targetOffsets));
  }
  await bridge.mouse("release", { settleFrames: 2 });
  return samples;
}

/** The change of any offset from one sample to the next, in metres. */
const steps = (samples: Vec3[][]) =>
  samples.slice(1).map((s, i) => Math.max(...s.flatMap((c, k) => c.map((v, a) => Math.abs(v - samples[i][k][a])))));
const largestStep = (samples: Vec3[][]) => Math.max(...steps(samples));

/** A jump: one mouse step that changes the shape far more than the typical step of the same drag. */
function expectNoJump(samples: Vec3[][], what: string) {
  const moving = steps(samples).filter((s) => s > 0).sort((a, b) => a - b);
  if (moving.length < 3) return;
  const median = moving[Math.floor(moving.length / 2)];
  expect(moving.at(-1)!, `${what}: steps ${steps(samples).map((s) => s.toFixed(1)).join(" ")}`).toBeLessThanOrEqual(median * 2.5 + 0.2);
}

describe("DRAG – dragging with the mouse", () => {
  useInputCity();

  it("DRAG-004 dragging an arrow seen straight down causes no jumps", async (context) => {
    const house = await building(context, "plainHouse");
    await target(house, "Face", Face.top);
    await bridge.setCamera({ focus: house, yaw: 0, pitch: 89, distance: 120 });
    const samples = await sampledDrag("arrowY", 0, -20, 10);
    const heights = samples.map((s) => s[corner(1, 1, 1)][1]);
    expect(largestStep(samples), `no jumps: ${heights.join(" ")}`).toBeLessThan(3);
    expectNoJump(samples, "arrow seen straight down");
    for (const sample of samples) for (const c of [0, 1, 4, 5]) expect(sample[c], "only the top moves").toEqual([0, 0, 0]);
  });

  it("DRAG-005 pressing on a handle and dragging selects and moves it horizontally; one undo; a click only selects", async (context) => {
    const house = await building(context, "plainHouse");
    for (const preset of ["front", "street"] as Preset[]) {
      await bridge.select(house);
      await bridge.pick("Corner");
      await lookAt(house, preset, 110);
      const at = await bridge.screenPoint({ handle: "Corner7" });
      await bridge.mouse("drag", { at: { handle: "Corner7" }, to: { x: at.x! + 150, y: at.y! }, frames: 10, settleFrames: 3 });
      const cage = await bridge.settled();
      const moved = offsets(cage.targetOffsets)[7];
      expect((await bridge.session()).handle, `${preset}: the pressed handle is selected`).toBe("Corner7");
      expect(moved[1], `${preset}: height kept`).toBe(0);
      expect(Math.hypot(moved[0], moved[2]), `${preset}: moved horizontally`).toBeGreaterThan(0.5);
      await bridge.undo();
      expect(allZero((await bridge.settled()).targetOffsets), `${preset}: one undo restores it`).toBe(true);
      await bridge.stepBack();
      await bridge.stepBack();
    }
    await bridge.select(house);
    await bridge.pick("Corner");
    await bridge.mouse("click", { at: { handle: "Corner6" }, settleFrames: 3 });
    expect((await bridge.session()).handle, "a click selects").toBe("Corner6");
    expect(allZero((await bridge.session()).cage!.targetOffsets), "and moves nothing").toBe(true);
  });

  it("DRAG-006 the square moves the handle in the horizontal plane", async (context) => {
    const house = await building(context, "plainHouse");
    await target(house, "Face", Face.top);
    await lookAt(house, "front", 110);
    const plane = await bridge.screenPoint({ gizmo: "plane" });
    const origin = plane.gizmo!.origin;
    const xTip = (await bridge.screenPoint({ gizmo: "arrowX" })).world;
    const x = [xTip[0] - origin[0], 0, xTip[2] - origin[2]];
    const n = Math.hypot(x[0], x[2]);
    const goal: Vec3 = [plane.world[0] + (x[0] / n) * 2, plane.world[1], plane.world[2] + (x[2] / n) * 2];
    await bridge.mouse("drag", { at: { gizmo: "plane" }, to: { world: goal }, frames: 10, settleFrames: 3 });
    const moved = offsets((await bridge.settled()).targetOffsets);
    for (const c of Top) {
      expect(moved[c][0], `corner ${c} along X`).toBeCloseTo(2, 0);
      expect(moved[c][1], `corner ${c} height kept`).toBe(0);
      expect(Math.abs(moved[c][2]), `corner ${c} not along Z`).toBeLessThan(0.3);
    }
  });

  it("DRAG-008 the red and blue rings slope the roof without jumps, also seen edge-on", async (context) => {
    const house = await building(context, "plainHouse");
    await bridge.gizmo("Rotate");
    // Ring X (red) lies in the YZ plane: edge-on from the street side; ring Z (blue) in XY: edge-on from a side.
    for (const [ring, yaw, slope] of [
      ["ringX", 0, (s: Vec3[]) => s[corner(0, 1, 1)][1] - s[corner(0, 1, 0)][1]],
      ["ringX", 90, (s: Vec3[]) => s[corner(0, 1, 1)][1] - s[corner(0, 1, 0)][1]],
      ["ringZ", 90, (s: Vec3[]) => s[corner(1, 1, 0)][1] - s[corner(0, 1, 0)][1]],
      ["ringZ", 0, (s: Vec3[]) => s[corner(1, 1, 0)][1] - s[corner(0, 1, 0)][1]],
    ] as [GizmoPart, number, (s: Vec3[]) => number][]) {
      await target(house, "Face", Face.top);
      await bridge.setCamera({ focus: house, yaw, pitch: 12, distance: 110 });
      const samples = await sampledDrag(ring, 0, 4, 10);
      const slopes = samples.map(slope);
      expect(Math.abs(slopes.at(-1)!), `${ring} at yaw ${yaw} slopes the roof: ${slopes.join(" ")}`).toBeGreaterThan(0.3);
      expectNoJump(samples, `${ring} at yaw ${yaw}`);
      await bridge.undo();
      await bridge.settled();
      await bridge.stepBack();
    }
  });
});
