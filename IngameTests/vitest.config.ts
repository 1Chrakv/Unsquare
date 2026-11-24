import { defineConfig } from "vitest/config";

// One game, one city: everything runs in one process, one file and one test at a time.
const set = process.env.UQ_SET ?? "fast";
const include =
  set === "slow" ? ["specs/slow/**/*.spec.ts"] : set === "all" ? ["specs/**/*.spec.ts"] : ["specs/*.spec.ts"];

export default defineConfig({
  test: {
    include,
    globals: true,
    pool: "forks",
    poolOptions: { forks: { singleFork: true } },
    fileParallelism: false,
    sequence: { concurrent: false, shuffle: false },
    testTimeout: 180_000,
    hookTimeout: 1_200_000,
    bail: 0,
    reporters: ["default", "json"],
    outputFile: { json: process.env.UQ_RESULTS ?? "../.testruns/latest/results.json" },
  },
});
