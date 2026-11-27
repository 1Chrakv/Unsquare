import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { bridge, type CameraJson } from "./bridge.js";

/** Fixed views of a building: yaw relative to its street front, pitch in degrees. Distance comes from its size. */
export const Presets = {
  front: { yaw: 0, pitch: 30 },
  back: { yaw: 180, pitch: 30 },
  left: { yaw: -90, pitch: 30 },
  right: { yaw: 90, pitch: 30 },
  top: { yaw: 0, pitch: 80 },
  street: { yaw: 0, pitch: 12 },
} as const;
export type Preset = keyof typeof Presets;

/** Folder of the run's screenshots (a temp folder outside a run). */
export const shotsDir = () => path.join(process.env.UNSQUARE_RUN_DIR ?? path.join(os.tmpdir(), "unsquare-shots"), "shots");

/**
 * Points the camera at a building from a preset. Without a distance it frames the target's cage (or 60 m when the
 * building isn't the target).
 */
export async function lookAt(ref: string, preset: Preset = "front", distance?: number): Promise<CameraJson> {
  const session = await bridge.session();
  const box = session.target?.ref === ref || session.target?.id === ref ? session.cage?.box : undefined;
  const size = box ? Math.hypot(box.max[0] - box.min[0], box.max[1] - box.min[1], box.max[2] - box.min[2]) : 0;
  return bridge.setCamera({ focus: ref, ...Presets[preset], distance: distance ?? (size > 0 ? Math.max(40, size * 2.2) : 60) });
}

/**
 * Takes a screenshot and copies it into the run's `shots/` folder; returns the local path. `crop: "target"` frames the
 * edited cage; `ui: false` hides the game's UI for the shot.
 */
export async function shot(name: string, options: Parameters<typeof bridge.screenshot>[1] = {}): Promise<string> {
  const taken = await bridge.screenshot(name, { maxPx: 1280, ...options });
  const dir = shotsDir();
  fs.mkdirSync(dir, { recursive: true });
  const local = path.join(dir, `${name}.jpg`);
  fs.copyFileSync(taken.path, local);
  return local;
}
