import fs from "node:fs";
import path from "node:path";
import { runsDir } from "./paths.js";
import { idOf, readRequirements } from "./requirements.js";

interface AssertionResult {
  title: string;
  fullName: string;
  status: "passed" | "failed" | "skipped" | "pending" | "todo";
  duration?: number;
  failureMessages?: string[];
  meta?: { skipReason?: string };
}

interface JsonResults {
  numPassedTests: number;
  numFailedTests: number;
  numPendingTests: number;
  testResults: { name: string; status: string; message?: string; assertionResults: AssertionResult[] }[];
}

/** Turns Vitest's JSON results into report.md: one line per requirement test, failures with their message. */
export function writeReport(resultsFile: string, reportFile: string, set: string): string {
  const results = JSON.parse(fs.readFileSync(resultsFile, "utf8")) as JsonResults;
  const requirements = new Map(readRequirements().map((r) => [r.id, r]));
  const lines = [
    `# In-game test run (${set})`,
    "",
    `${new Date().toISOString()}: ${results.numPassedTests} passed, ${results.numFailedTests} failed, ${results.numPendingTests} skipped.`,
    "",
    "| Requirement | Result | Test | Time |",
    "|---|---|---|---|",
  ];
  const failures: string[] = [];
  for (const file of results.testResults) {
    if (file.assertionResults.length === 0 && file.message)
      failures.push(`### ${file.name}\n\n\`\`\`\n${file.message}\n\`\`\``);
    for (const test of file.assertionResults) {
      const id = idOf(test.title) ?? "–";
      const result = test.status === "passed" ? "pass" : test.status === "failed" ? "**FAIL**" : "skip";
      const reason = test.meta?.skipReason ? ` (${test.meta.skipReason})` : "";
      const seconds = test.duration ? `${(test.duration / 1000).toFixed(1)} s` : "";
      lines.push(`| ${id} | ${result}${reason} | ${test.title.replace(/\|/g, "/")} | ${seconds} |`);
      if (test.status === "failed")
        failures.push(
          `### ${test.title}\n\n${requirements.get(id ?? "")?.text ?? ""}\n\n\`\`\`\n${(test.failureMessages ?? []).join("\n").slice(0, 4000)}\n\`\`\``,
        );
    }
  }
  lines.push(...reviewSection(path.dirname(reportFile)));
  if (failures.length > 0) lines.push("", "## Failures", "", ...failures);
  fs.writeFileSync(reportFile, lines.join("\n") + "\n");
  return reportFile;
}

export interface Verdict {
  fingerprint: string;
  verdict: "pass" | "fail";
  reason: string;
  evidence: string;
  date: string;
}

/** Claude's review verdicts (`/ingame-review`), by requirement ID; kept across runs. */
export const verdictsFile = path.join(runsDir, "verdicts.json");

export function readVerdicts(): Record<string, Verdict> {
  return fs.existsSync(verdictsFile) ? (JSON.parse(fs.readFileSync(verdictsFile, "utf8")) as Record<string, Verdict>) : {};
}

/** The run's review packs with the verdict that matches their fingerprint, or "waiting for review". */
function reviewSection(runDir: string): string[] {
  const dir = path.join(runDir, "review");
  if (!fs.existsSync(dir)) return [];
  const verdicts = readVerdicts();
  const lines = ["", "## Review packs", "", "| Requirement | Verdict | Reason |", "|---|---|---|"];
  for (const id of fs.readdirSync(dir).sort()) {
    const fingerprintFile = path.join(dir, id, "fingerprint");
    if (!fs.existsSync(fingerprintFile)) continue;
    const verdict = verdicts[id];
    const current = verdict && verdict.fingerprint === fs.readFileSync(fingerprintFile, "utf8").trim();
    lines.push(
      current
        ? `| ${id} | ${verdict.verdict === "pass" ? "pass" : "**FAIL**"} | ${verdict.reason.replace(/\|/g, "/")} |`
        : `| ${id} | waiting for review | run \`/ingame-review\` |`,
    );
  }
  return lines;
}
