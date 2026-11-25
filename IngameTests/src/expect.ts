import { expect } from "vitest";
import type { CageJson, Vec3 } from "./bridge.js";

/** Asserts two points are within `tolerance` metres of each other, naming the axis that is off. */
export function expectNear(actual: Vec3, expected: Vec3, tolerance = 0.05, what = "point") {
  const distance = Math.hypot(actual[0] - expected[0], actual[1] - expected[1], actual[2] - expected[2]);
  expect(distance, `${what}: ${fmt(actual)} should be within ${tolerance} m of ${fmt(expected)}`).toBeLessThanOrEqual(tolerance);
}

/** Corner offsets in metres from a cage's offsets string (`targetOffsets`: "x,y,z;…" in 0.1 m steps). */
export function offsets(text: string): Vec3[] {
  return text.split(";").map((corner) => corner.split(",").map((step) => Number(step) / 10) as Vec3);
}

/** Cage faces by index: -X, +X (left seen from the street), -Y, +Y (top), -Z (back), +Z (street side). */
export const Face = { minusX: 0, plusX: 1, bottom: 2, top: 3, back: 4, front: 5 } as const;

/** Corner positions of the cage, rounded to the 0.1 m grid the mod quantizes to. */
export const corners = (cage: CageJson): Vec3[] => cage.corners.map((c) => c.map((v) => Math.round(v * 10) / 10) as Vec3);

/** Asserts the cage's applied shape equals the requested one and nothing is pending. */
export function expectApplied(cage: CageJson) {
  expect(cage.status, `cage status (applied ${cage.appliedSpec}, target ${cage.targetSpec})`).toBe("Applied");
  expect(cage.appliedSpec).toBe(cage.targetSpec);
}

export function expectUndeformed(cage: CageJson) {
  expect(cage.deformed, `cage should be undeformed: ${cage.targetSpec}`).toBe(false);
  expect(cage.status).toBe("Undeformed");
}

/**
 * Where a building-space point of the undeformed box ends up under the cage (trilinear, like the mod's deformation;
 * points outside the box extrapolate). Use the cage of the same snapshot for box and corners.
 */
export function cageMap(cage: CageJson, point: Vec3, box = cage.box, deformed = cage.corners): Vec3 {
  const t = [0, 1, 2].map((a) => (point[a] - box.min[a]) / (box.max[a] - box.min[a]));
  const result: Vec3 = [0, 0, 0];
  for (let c = 0; c < 8; c++) {
    const w = [0, 1, 2].reduce((weight, a) => weight * (((c >> a) & 1) === 1 ? t[a] : 1 - t[a]), 1);
    for (let a = 0; a < 3; a++) result[a] += w * deformed[c][a];
  }
  return result;
}

/** Yaw in degrees of a building-space direction (0 = +Z street side, 90 = +X). */
export const yaw = (direction: Vec3) => (Math.atan2(direction[0], direction[2]) * 180) / Math.PI;

/** Smallest signed difference of two angles in degrees. */
export const angleDiff = (a: number, b: number) => ((((a - b) % 360) + 540) % 360) - 180;

export const distanceXZ = (a: Vec3, b: Vec3) => Math.hypot(a[0] - b[0], a[2] - b[2]);

/** Edge index of the cage edge between two corners that differ in one axis bit (edges grouped by axis, 4 each). */
export function edge(a: number, b: number): number {
  const bit = a ^ b;
  const axis = [1, 2, 4].indexOf(bit);
  if (axis < 0) throw new Error(`Corners ${a} and ${b} don't share an edge.`);
  const low = Math.min(a, b);
  const starts = [0, 1, 2, 3, 4, 5, 6, 7].filter((c) => (c & bit) === 0);
  return axis * 4 + starts.indexOf(low);
}

/** Cage corner indices: bit 0 = +X, bit 1 = +Y (top), bit 2 = +Z (street side). */
export const corner = (x: 0 | 1, y: 0 | 1, z: 0 | 1) => x + 2 * y + 4 * z;

const fmt = (v: Vec3) => `(${v.map((n) => n.toFixed(2)).join(", ")})`;

/** Ground areas keep their outline in plan; their heights follow the terrain, so y is not compared. */
export function expectSamePlan(after: { local: Vec3[] }[], before: { local: Vec3[] }[], tolerance = 0.05) {
  expect(after.length, "area count").toBe(before.length);
  before.forEach((area, i) => {
    expect(after[i].local.length, `points of area ${i}`).toBe(area.local.length);
    area.local.forEach((p, j) => expect(distanceXZ(after[i].local[j], p), `area ${i} point ${j}`).toBeLessThan(tolerance));
  });
}
