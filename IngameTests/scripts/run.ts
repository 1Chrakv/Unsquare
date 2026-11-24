// Runs a test set against the running game (starting it if needed) and writes .testruns/<stamp>/report.md.
// Usage: tsx scripts/run.ts fast|slow|all [vitest args, e.g. -t UNDO-]
import { spawnSync } from "node:child_process";
import fs from "node:fs";
import path from "node:path";
import { runsDir, testsDir } from "../src/paths.js";
import { writeReport } from "../src/report.js";

const [set = "fast", ...vitestArgs] = process.argv.slice(2);
const stamp = new Date().toISOString().replace(/[:.]/g, "-").slice(0, 19);
const runDir = path.join(runsDir, stamp);
fs.mkdirSync(runDir, { recursive: true });
const results = path.join(runDir, "results.json");

const vitest = spawnSync(process.execPath, [path.join(testsDir, "node_modules", "vitest", "vitest.mjs"), "run", ...vitestArgs], {
  cwd: testsDir,
  stdio: "inherit",
  env: { ...process.env, UQ_SET: set, UQ_RESULTS: results, UQ_RUN_DIR: runDir },
});

if (fs.existsSync(results)) {
  const report = writeReport(results, path.join(runDir, "report.md"), set);
  console.log(`\nReport: ${report}`);
  fs.rmSync(path.join(runsDir, "latest"), { recursive: true, force: true });
  fs.cpSync(runDir, path.join(runsDir, "latest"), { recursive: true });
} else console.error("No results were written; see the output above.");
process.exit(vitest.status ?? 1);
