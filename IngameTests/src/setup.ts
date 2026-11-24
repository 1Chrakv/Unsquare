import { afterEach, beforeAll } from "vitest";
import { bridge, BridgeError, type BuildingJson, type HandleKind } from "./bridge.js";
import { cleanUp, loadFixture } from "./fixture.js";
import { LogWatch } from "./log.js";

/**
 * Registers the usual hooks of a spec file: the fixture city is loaded (once per run) with Unsquare mode on, and
 * after every test the city is put back (nothing deformed, no target, empty history).
 */
export function useCity() {
  beforeAll(async () => {
    await loadFixture();
    await cleanUp();
  });
  afterEach(async () => {
    await cleanUp();
  });
}

/** Selects a building and one of its handles, as a click on the building and one on the handle would. */
export async function target(ref: string, kind?: HandleKind, index = 0) {
  await bridge.select(ref);
  if (kind) await bridge.handle(kind, index);
}

/** Moves a handle of the target by `delta` metres (building space) in one drag, then waits for the bake. */
export async function move(kind: HandleKind, index: number, delta: [number, number, number]) {
  await bridge.handle(kind, index);
  await bridge.drag({ translate: delta, frames: 3 });
  return bridge.settled();
}

/** Ground points on a grid over the lot box, inset from its edges (building space x, z). */
export function lotGrid(cage: { lotBox: { min: number[]; max: number[] } }, steps = 4, inset = 1): [number, number][] {
  const { min, max } = cage.lotBox;
  const points: [number, number][] = [];
  for (let i = 0; i <= steps; i++)
    for (let j = 0; j <= steps; j++)
      points.push([
        min[0] + inset + ((max[0] - min[0] - 2 * inset) * i) / steps,
        min[2] + inset + ((max[2] - min[2] - 2 * inset) * j) / steps,
      ]);
  return points;
}

/** The bridge reply's error result (e.g. "Rejected") of a command expected to be refused. */
export async function refused(promise: Promise<unknown>): Promise<string> {
  try {
    await promise;
  } catch (error) {
    if (error instanceof BridgeError) return (error.reply.result as { result?: string })?.result ?? error.reply.error ?? "";
    throw error;
  }
  throw new Error("The command was expected to be refused, but it succeeded.");
}

/** Two deformable, plain buildings of the same prefab, for variant reuse tests. */
export async function twoOfAKind(): Promise<[BuildingJson, BuildingJson] | undefined> {
  const all = (await bridge.find({ variant: false, limit: 400 })).filter((b) => b.deformable);
  const byPrefab = new Map<string, BuildingJson[]>();
  for (const building of all) byPrefab.set(building.prefab, [...(byPrefab.get(building.prefab) ?? []), building]);
  for (const group of byPrefab.values()) if (group.length >= 2) return [group[0], group[1]];
  return undefined;
}

/** Bake lines ("Bake for building …: <variant>, N ms, M new assets, …") written since the watch started. */
export function bakes(log: LogWatch) {
  return log.matching(/Bake for building/).map((line) => ({
    line,
    ms: Number(/, (\d+) ms/.exec(line)?.[1] ?? NaN),
    newAssets: Number(/, (\d+) new assets/.exec(line)?.[1] ?? NaN),
    variant: /: (\S+__uq[0-9a-f]+)/.exec(line)?.[1],
  }));
}
