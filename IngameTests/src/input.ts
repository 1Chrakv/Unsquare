import { bridge, type GizmoPart, type PointerTarget, type Vec3 } from "./bridge.js";
import { game } from "./game.js";

/**
 * Real mouse and keyboard input through the bridge's synthetic devices. Settings from the environment (the phase 3
 * spike decides the defaults): UNSQUARE_INPUT_VIA virtual|current, UNSQUARE_FORCE_FOCUS=1, UNSQUARE_FOCUS=1 (bring
 * the game window to the front before input).
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
