import { afterAll, beforeAll } from "vitest";
import { bridge, type GizmoPart, type PointerTarget, type Vec3 } from "./bridge.js";
import { game } from "./game.js";
import { useCity } from "./setup.js";

/**
 * Real mouse and keyboard input through the bridge's own virtual devices; works with the game in the background.
 * Environment: UNSQUARE_INPUT_VIA virtual|current, UNSQUARE_FORCE_FOCUS=0 (don't tell the game it has focus),
 * UNSQUARE_FOCUS=1 (bring the game window to the front first).
 */
export async function useInput() {
  if (process.env.UNSQUARE_FOCUS === "1") game.focus();
  await bridge.inputConfig({
    via: (process.env.UNSQUARE_INPUT_VIA as "virtual" | "current" | undefined) ?? "virtual",
    forceFocus: process.env.UNSQUARE_FORCE_FOCUS !== "0",
  });
}

/** Releases everything and hands input back to the player's devices. */
export const releaseInput = () => bridge.inputReset();

/** Moves the mouse there and returns what the tool hovers afterwards. */
export const hover = async (at: PointerTarget) => (await bridge.mouse("move", { at })).hover;

export const click = (at: PointerTarget, button: "left" | "right" = "left") => bridge.mouse("click", { at, button });

/** A right click where the pointer is (steps back in Unsquare mode). */
export const rightClick = () => bridge.mouse("click", { button: "right" });

/** Presses on a gizmo part of the selected handle and drags it to the screen point of `to` (a world point). */
export async function dragGizmo(part: GizmoPart, to: Vec3, frames = 12) {
  return bridge.mouse("drag", { at: { gizmo: part }, to: { world: to }, frames });
}

/** Drags from one target to another in screen space, optionally a pixel offset from the start. */
export async function drag(at: PointerTarget, to: PointerTarget | { dx: number; dy: number }, frames = 12) {
  if ("dx" in to) {
    const start = await bridge.mouse("move", { at });
    to = { x: start.pointer[0] + to.dx, y: start.pointer[1] + to.dy };
  }
  return bridge.mouse("drag", { at, to, frames });
}

/** Presses a binding by name (UnsquareUndo …), with its current keys. */
export const press = (binding: string) => bridge.key({ binding });

/** Presses raw keys together (Unity key names: LeftCtrl, Escape, Z). */
export const tap = (...keys: string[]) => bridge.key({ keys });

/** `useCity()` plus the input devices for a spec file of `specs/input/`. */
export function useInputCity() {
  useCity();
  beforeAll(useInput);
  afterAll(releaseInput);
}

/** A building-space point of `owner` (x width, y up, z street side) in world space; the inverse of `toLocal`. */
export function toWorld(owner: { position: Vec3; yawDegrees: number }, local: Vec3): Vec3 {
  const yaw = (owner.yawDegrees * Math.PI) / 180;
  const [x, y, z] = local;
  return [owner.position[0] + x * Math.cos(yaw) + z * Math.sin(yaw), owner.position[1] + y, owner.position[2] - x * Math.sin(yaw) + z * Math.cos(yaw)];
}

type Box = { min: Vec3; max: Vec3 };

/**
 * A point of a building given in its cage box (building space), as a pointer target. `place` gets the box and the
 * lot box and returns the local point; the building need not be selected.
 */
export async function pointOf(ref: string, place: (box: Box, lotBox: Box) => Vec3): Promise<{ world: Vec3 }> {
  const found = await bridge.building(ref);
  if (!found.cage) throw new Error(`${ref} has no cage (not deformable).`);
  return { world: toWorld(found, place(found.cage.box, found.cage.lotBox)) };
}

/** The middle of the building's box: a ray there hits the building first from any side. */
export const centreOf = (ref: string) =>
  pointOf(ref, (box) => [(box.min[0] + box.max[0]) / 2, (box.min[1] + box.max[1]) / 2, (box.min[2] + box.max[2]) / 2]);

/** Ground `metres` in front of the lot (street side), off every building. */
export const groundBefore = (ref: string, metres = 12) =>
  pointOf(ref, (_, lot) => [(lot.min[0] + lot.max[0]) / 2, 0, lot.max[2] + metres]);
