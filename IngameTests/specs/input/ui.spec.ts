import { describe, expect, it } from "vitest";
import { bridge, type HandleKind } from "../../src/bridge.js";
import { Face, offsets } from "../../src/expect.js";
import { building } from "../../src/fixture.js";
import { press, useInputCity } from "../../src/input.js";
import { LogWatch } from "../../src/log.js";
import { pack } from "../../src/review.js";
import { target } from "../../src/setup.js";
import { button, centre, clickButton, evaluate, iconCount, toolWindow, type Rect } from "../../src/ui.js";
import { lookAt, shot } from "../../src/view.js";

/** Screen y of the highest top corner and x of the roof centre of the target. */
async function roof() {
  const tops = await Promise.all(["Corner2", "Corner3", "Corner6", "Corner7"].map((handle) => bridge.screenPoint({ handle })));
  const middle = await bridge.screenPoint({ handle: "Face3" });
  return { x: middle.x!, top: Math.min(...tops.map((t) => t.y!)) };
}

/** The window is centred over the roof and above its highest corner (pixels of slack). */
async function expectAboveRoof(window: Rect, what: string) {
  const { x, top } = await roof();
  expect(Math.abs(centre(window).x - x), `${what}: centred over the roof`).toBeLessThan(window.width * 0.05 + 4);
  expect(window.y + window.height, `${what}: above the highest corner`).toBeLessThanOrEqual(top + 4);
}

const session = () => bridge.session();

describe("UI – the tool window and the mode button", () => {
  useInputCity();

  it("UI-001 no window without a target; with one it is centred above the roof", async (context) => {
    const house = await building(context, "plainHouse");
    await lookAt(house, "front", 130);
    expect(await toolWindow(), "no target, no window").toBeNull();
    const none = await shot("UI-001-none", { maxPx: 1600 });
    await bridge.select(house);
    await bridge.frames(3);
    const window = await toolWindow();
    expect(window, "the window shows").not.toBeNull();
    await expectAboveRoof(window!, "front view");
    await pack("UI-001", {
      pack: "toolwindow",
      shots: { none, selected: await shot("UI-001-selected", { maxPx: 1600 }) },
      probe: { window, roof: await roof() },
      focus: "The first shot has no tool window; the second shows it centred above the roof of the edited building.",
    });
  });

  it("UI-002 the window follows the roof, stops at the screen border and hides behind the camera", async (context) => {
    const house = await building(context, "plainHouse");
    await bridge.select(house);
    const views: Record<string, string> = {};
    for (const [name, yaw, distance] of [["rotated", 60, 130], ["zoomed", 60, 260], ["panned", -40, 180]] as const) {
      await lookAt(house, "front", distance);
      await bridge.setCamera({ focus: house, yaw, distance });
      await bridge.frames(3);
      const window = await toolWindow();
      expect(window, `${name}: shown`).not.toBeNull();
      await expectAboveRoof(window!, name);
      views[name] = await shot(`UI-002-${name}`, { maxPx: 1600 });
    }
    // The roof near the top edge: the window stops at the border instead of leaving the screen.
    const base = (await bridge.screenPoint({ handle: "Face2" })).world;
    await bridge.setCamera({ point: [base[0], base[1], base[2]], pitch: 5, distance: 60 });
    await bridge.frames(3);
    const clamped = await toolWindow();
    if (clamped) expect(clamped.y, "stops at the top border").toBeGreaterThanOrEqual(-1);
    views.border = await shot("UI-002-border", { maxPx: 1600 });
    // Looking away from the street side: the building is behind the camera and the window hides.
    const found = await bridge.building(house);
    const yaw = (found.yawDegrees * Math.PI) / 180;
    const front = [Math.sin(yaw), Math.cos(yaw)]; // building +Z in world xz (see toWorld)
    const ahead: [number, number, number] = [found.position[0] + front[0] * 160, found.position[1], found.position[2] + front[1] * 160];
    await bridge.setCamera({ point: ahead, yaw: (Math.atan2(front[0], front[1]) * 180) / Math.PI, pitch: 10, distance: 60 });
    await bridge.frames(3);
    expect(await toolWindow(), "hidden with the building behind the camera").toBeNull();
    await pack("UI-002", {
      pack: "toolwindow",
      shots: views,
      probe: { clamped },
      focus: "In every shot the window sits above the roof; in the border shot it stays fully on screen.",
    });
  });

  it("UI-003 the pick buttons switch the pick mode and the highlight follows it", async (context) => {
    const house = await building(context, "plainHouse");
    await bridge.select(house);
    await lookAt(house, "front", 130);
    const kinds: [HandleKind, string][] = [["Corner", "pick-corner"], ["Face", "pick-face"], ["Cage", "pick-cage"], ["Edge", "pick-edge"]];
    for (const [kind, icon] of kinds) {
      await clickButton(icon);
      expect((await session()).pickKind, `${icon} picks ${kind}`).toBe(kind);
      for (const [, other] of kinds) expect((await button(other))?.active, `${other} highlighted with ${kind}`).toBe(other === icon);
    }
    // A hotkey switch moves the highlight too.
    await press("UnsquareKindFace");
    expect((await button("pick-face"))?.active, "highlight follows a hotkey").toBe(true);
    await pack("UI-003", {
      pack: "toolwindow",
      shots: { face: await shot("UI-003-face", { maxPx: 1600 }) },
      probe: { pickKind: (await session()).pickKind },
      focus: "The Face pick button is the highlighted one in the Pick section.",
    });
  });

  it("UI-004 the reset-part button is disabled without a handle and resets only the selected handle, undoable", async (context) => {
    const house = await building(context, "plainHouse");
    await bridge.select(house);
    await lookAt(house, "front", 130);
    expect((await button("reset-handle"))?.disabled, "disabled without a handle").toBe(true);
    await target(house, "Face", Face.front);
    await bridge.nudge([0, 0, 1]);
    await bridge.handle("Face", Face.top);
    await bridge.nudge([0, 1, 0]);
    await bridge.settled();
    expect((await button("reset-handle"))?.disabled).toBe(false);
    const before = await shot("UI-004-before", { maxPx: 1600 });
    await clickButton("reset-handle");
    const cage = await bridge.settled();
    const after = offsets(cage.targetOffsets);
    for (const c of [2, 3, 6, 7]) expect(after[c][1], `top corner ${c} back down`).toBe(0);
    // Corners 6 and 7 belong to both faces and go back with the top; the bottom front corners stay out.
    for (const c of [4, 5]) expect(after[c][2], `front corner ${c} still out`).toBeCloseTo(0.1, 5);
    await bridge.undo();
    expect(offsets((await bridge.settled()).targetOffsets)[7][1], "undo brings the top back").toBeCloseTo(0.1, 5);
    await pack("UI-004", {
      pack: "toolwindow",
      shots: { before, after: await shot("UI-004-after", { maxPx: 1600 }) },
      probe: { offsets: cage.targetOffsets },
      focus: "The reset-part button looks enabled while a handle is selected (it was greyed before the handle).",
    });
  });

  it("UI-005 the reset-building button restores the original shape, undoable", async (context) => {
    const house = await building(context, "plainHouse");
    await target(house, "Face", Face.top);
    await lookAt(house, "front", 130);
    await bridge.nudge([0, 1, 0]);
    await bridge.nudge([1, 0, 0]);
    const shaped = (await bridge.settled()).targetOffsets;
    const log = LogWatch.fromNow();
    await clickButton("reset-building");
    expect((await bridge.settled()).deformed, "back to the original").toBe(false);
    await log.waitFor(/\[Edit\] Reset \(\w+\) -> Accepted/);
    await bridge.undo();
    expect((await bridge.settled()).targetOffsets, "undo brings the shape back").toBe(shaped);
  });

  it("UI-006 a click on a window button keeps selection and target and reaches neither building nor ground", async (context) => {
    const house = await building(context, "plainHouse");
    await target(house, "Face", Face.top);
    await lookAt(house, "front", 130);
    const before = await session();
    const log = LogWatch.fromNow();
    // The window floats over ground far from the cage: a click reaching the world there would step back.
    await clickButton("gizmo-move");
    await clickButton("undo");
    const after = await session();
    expect(after.handle, "selection kept").toBe(before.handle);
    expect(after.target?.id, "target kept").toBe(before.target?.id);
    expect(log.matching(/\[Edit\] (Selection cleared|Target cleared|Target )/), "nothing reached the world").toEqual([]);
  });

  it("UI-007 the window hides with the target and outside the mode", async (context) => {
    const house = await building(context, "plainHouse");
    await target(house, "Face", Face.top);
    await lookAt(house, "front", 130);
    expect(await toolWindow()).not.toBeNull();
    await bridge.stepBack();
    expect(await toolWindow(), "still there with the target").not.toBeNull();
    await bridge.stepBack();
    await bridge.frames(3);
    expect(await toolWindow(), "hidden without a target").toBeNull();
    await bridge.select(house);
    await bridge.frames(3);
    await bridge.mode(false);
    await bridge.frames(3);
    expect(await toolWindow(), "hidden outside the mode").toBeNull();
    const off = await shot("UI-007-off", { maxPx: 1600 });
    await bridge.mode(true);
    await pack("UI-007", {
      pack: "toolwindow",
      shots: { off },
      focus: "Unsquare mode is off: no tool window, no cage, the mode button not highlighted.",
    });
  });

  it("UI-008 the gizmo toggle sits under the pick toggle and switches like a Ctrl tap", async (context) => {
    const house = await building(context, "plainHouse");
    await target(house, "Face", Face.top);
    await lookAt(house, "front", 130);
    const pickLeft = (await button("pick-corner"))!;
    const pickRight = (await button("pick-cage"))!;
    const move = (await button("gizmo-move"))!;
    const rotate = (await button("gizmo-rotate"))!;
    expect(move.y, "below the pick toggle").toBeGreaterThan(pickLeft.y + pickLeft.height - 1);
    expect(move.x, "within the pick section").toBeGreaterThanOrEqual(pickLeft.x - 1);
    expect(rotate.x + rotate.width, "within the pick section").toBeLessThanOrEqual(pickRight.x + pickRight.width + 1);
    await clickButton("gizmo-rotate");
    expect((await session()).gizmo).toBe("Rotate");
    expect((await session()).showsRings, "rings like after a Ctrl tap").toBe(true);
    await pack("UI-008", {
      pack: "toolwindow",
      shots: { rotate: await shot("UI-008-rotate", { maxPx: 1600 }) },
      probe: { pickLeft, move, rotate },
      focus: "The move/rotate toggle sits right under the pick buttons, Rotate highlighted, and the gizmo shows rings.",
    });
    await clickButton("gizmo-move");
  });

  it("UI-009 switching the align section to height turns side buttons into level buttons and dims the align mode", async (context) => {
    const house = await building(context, "turnedNeighbour");
    await target(house, "Face", Face.top);
    await lookAt(house, "front", 130);
    const dimmed = () =>
      evaluate<boolean>(`(() => { const t = document.querySelector('img[src$="/align-angle.svg"]').closest('[class^="track_"]'); return /trackDisabled_/.test(t.className); })()`);
    expect((await button("align-left"))?.visible, "align buttons first").toBe(true);
    expect(await dimmed()).toBe(false);
    await clickButton("align-kind-height");
    expect((await button("align-ground-left"))?.visible, "level buttons").toBe(true);
    expect(await button("align-left"), "align buttons gone").toBeNull();
    expect(await dimmed(), "align mode toggle dimmed").toBe(true);
    const height = await shot("UI-009-height", { maxPx: 1600 });
    const log = LogWatch.fromNow();
    await clickButton("align-ground-left");
    await log.waitFor(/\[Edit\] Level Left with neighbour/);
    await bridge.settled();
    await clickButton("align-kind-position");
    expect((await button("align-left"))?.visible, "align buttons back").toBe(true);
    expect(await dimmed()).toBe(false);
    await pack("UI-009", {
      pack: "toolwindow",
      shots: { height },
      focus: "In height mode the four side buttons show level icons and the angle/flush/full toggle is dimmed.",
    });
  });

  it("UI-010 the window's undo and redo buttons work and are disabled when there is nothing to do", async (context) => {
    const house = await building(context, "plainHouse");
    await target(house, "Face", Face.top);
    await lookAt(house, "front", 130);
    expect((await button("undo"))?.disabled, "nothing to undo").toBe(true);
    expect((await button("redo"))?.disabled, "nothing to redo").toBe(true);
    await bridge.nudge([0, 1, 0]);
    const raised = (await bridge.settled()).targetOffsets;
    expect((await button("undo"))?.disabled).toBe(false);
    await clickButton("undo");
    expect((await bridge.settled()).deformed, "undone").toBe(false);
    expect((await button("undo"))?.disabled, "nothing left to undo").toBe(true);
    expect((await button("redo"))?.disabled).toBe(false);
    const undone = await shot("UI-010-undone", { maxPx: 1600 });
    await clickButton("redo");
    expect((await bridge.settled()).targetOffsets, "redone").toBe(raised);
    expect((await button("redo"))?.disabled).toBe(true);
    await pack("UI-010", {
      pack: "toolwindow",
      shots: { undone },
      focus: "After the undo: undo greyed out, redo enabled.",
    });
  });

  it("UI-011 the button position option moves the button at once; one button, highlighted while on, toggles everywhere", async () => {
    const saved = (await bridge.settings()).ButtonPosition;
    const shots: Record<string, string> = {};
    try {
      for (const position of ["TopLeft", "TopRight", "Bottom"]) {
        await bridge.setSettings({ ButtonPosition: position });
        await bridge.frames(5);
        expect(await iconCount("unsquare-mode"), `${position}: shown once`).toBe(1);
        const found = (await button("unsquare-mode"))!;
        const { width, height } = (await bridge.screenPoint({ world: [0, 0, 0] })).screen;
        const where = { x: centre(found).x / width, y: centre(found).y / height };
        if (position === "TopLeft") expect(where.x < 0.5 && where.y < 0.5, `${position}: ${JSON.stringify(where)}`).toBe(true);
        if (position === "TopRight") expect(where.x > 0.5 && where.y < 0.5, `${position}: ${JSON.stringify(where)}`).toBe(true);
        if (position === "Bottom") expect(where.y > 0.5, `${position}: ${JSON.stringify(where)}`).toBe(true);
        expect(found.active, `${position}: highlighted while on`).toBe(true);
        await clickButton("unsquare-mode");
        expect((await session()).mode, `${position}: the button toggles`).toBe(false);
        expect((await button("unsquare-mode"))?.active).toBe(false);
        await clickButton("unsquare-mode");
        expect((await session()).mode).toBe(true);
        shots[position] = await shot(`UI-011-${position}`, { maxPx: 1600 });
      }
    } finally {
      await bridge.setSettings({ ButtonPosition: saved });
      if (!(await session()).mode) await bridge.mode(true);
    }
    await pack("UI-011", {
      pack: "toolwindow",
      shots,
      focus: "Each shot shows the Unsquare button once, highlighted, at the named place (top left, top right, bottom toolbar).",
    });
  });
});
