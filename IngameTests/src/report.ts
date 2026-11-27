import fs from "node:fs";
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
  if (failures.length > 0) lines.push("", "## Failures", "", ...failures);
  fs.writeFileSync(reportFile, lines.join("\n") + "\n");
  return reportFile;
}
