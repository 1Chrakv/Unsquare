import fs from "node:fs";
import path from "node:path";
import { requirementsFile, testsDir } from "./paths.js";

export interface Requirement {
  id: string;
  group: string;
  text: string;
  rows: string;
  check: string;
  /** The spec file named in the group heading, relative to IngameTests. */
  spec: string;
}

/** Parses Unsquare/Requirements.md: one table per group, headed "## GROUP – Title (`specs/x.spec.ts`)". */
export function readRequirements(file = requirementsFile): Requirement[] {
  const requirements: Requirement[] = [];
  let group = "";
  let spec = "";
  for (const line of fs.readFileSync(file, "utf8").split(/\r?\n/)) {
    const heading = /^## (\w+) – .*\(`([^`]+)`\)/.exec(line);
    if (heading) {
      [, group, spec] = heading;
      continue;
    }
    const row = /^\| ([A-Z]+-\d{3}) \| (.*) \| (.*) \| (.*) \|$/.exec(line);
    if (row) requirements.push({ id: row[1], group, text: row[2], rows: row[3], check: row[4], spec });
  }
  return requirements;
}

/** The kind of check: suite, suite-slow, review (suite+review) or claude. */
export function checkKind(requirement: Requirement): "suite" | "suite-slow" | "review" | "claude" {
  const check = requirement.check;
  if (check.startsWith("claude:")) return "claude";
  if (check.includes("+review")) return "review";
  if (check.startsWith("suite-slow")) return "suite-slow";
  return "suite";
}

export const needsInput = (requirement: Requirement) => /\binput\b/.test(requirement.check);

/** Old checklist rows a requirement names (ranges expanded; "Setup 3" and "Hotkeys" are not rows). */
export function oldRows(requirement: Requirement): number[] {
  const rows: number[] = [];
  for (const part of requirement.rows.split(",")) {
    const match = /^\s*(\d+)(?:[–-](\d+))?\s*$/.exec(part);
    if (!match) continue;
    const from = Number(match[1]);
    const to = match[2] ? Number(match[2]) : from;
    for (let row = from; row <= to; row++) rows.push(row);
  }
  return rows;
}

/** Requirement IDs that tests name: `it("UNDO-001 …")` in any spec file. */
export function testedIds(dir = path.join(testsDir, "specs")): Map<string, string[]> {
  const found = new Map<string, string[]>();
  const walk = (folder: string) => {
    for (const entry of fs.readdirSync(folder, { withFileTypes: true })) {
      const full = path.join(folder, entry.name);
      if (entry.isDirectory()) walk(full);
      else if (entry.name.endsWith(".spec.ts"))
        for (const match of fs.readFileSync(full, "utf8").matchAll(/\b(?:it|test)(?:\.\w+)?\(\s*["'`]([A-Z]+-\d{3})\b/g)) {
          const files = found.get(match[1]) ?? [];
          files.push(path.relative(testsDir, full).replace(/\\/g, "/"));
          found.set(match[1], files);
        }
    }
  };
  if (fs.existsSync(dir)) walk(dir);
  return found;
}

/** The ID a test name starts with, if any. */
export const idOf = (testName: string) => /^([A-Z]+-\d{3})\b/.exec(testName)?.[1];
