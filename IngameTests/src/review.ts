import crypto from "node:crypto";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { bridge } from "./bridge.js";
import type { LogWatch } from "./log.js";
import { testsDir } from "./paths.js";
import { readRequirements } from "./requirements.js";

export type PackKind = "mesh" | "overlay" | "toolwindow" | "ground" | "foreign";

export interface PackInput {
  pack: PackKind;
  /** Screenshots already in the run (from `view.shot`), by role: before, after, front, top … */
  shots: Record<string, string>;
  /** Measured values the reviewer can check the pictures against; part of the fingerprint. */
  probe?: unknown;
  /** The building whose meshes go into the fingerprint (a new bake gives a new fingerprint). */
  building?: string;
  /** What exactly to look for in this test, beyond the pack's checklist. */
  focus?: string;
  log?: LogWatch;
}

/** Folder of the run's review packs (a temp folder outside a run). */
export const reviewDir = () => path.join(process.env.UNSQUARE_RUN_DIR ?? path.join(os.tmpdir(), "unsquare-review"), "review");

/**
 * Writes the evidence for Claude's review of a requirement the suite can only partly judge: brief.md (the requirement,
 * the pack's checklist, what to look for), the shots, probe.json, log.txt and a fingerprint. `/ingame-review` reuses
 * a verdict while the fingerprint stays the same.
 */
export async function pack(id: string, input: PackInput): Promise<string> {
  const requirement = readRequirements().find((r) => r.id === id);
  if (!requirement) throw new Error(`No requirement ${id}.`);
  const template = fs.readFileSync(path.join(testsDir, "review", `${input.pack}.md`), "utf8");
  const dir = path.join(reviewDir(), id);
  fs.rmSync(dir, { recursive: true, force: true });
  fs.mkdirSync(dir, { recursive: true });

  const shots: string[] = [];
  for (const [role, file] of Object.entries(input.shots)) {
    const name = `${role}${path.extname(file) || ".jpg"}`;
    fs.copyFileSync(file, path.join(dir, name));
    shots.push(name);
  }
  const meshes = input.building ? await bridge.meshes(input.building) : null;
  const probe = { probe: input.probe ?? null, meshes };
  fs.writeFileSync(path.join(dir, "probe.json"), JSON.stringify(probe, null, 2));
  if (input.log) fs.writeFileSync(path.join(dir, "log.txt"), input.log.text());

  // Same requirement, checklist, measurements and baked meshes: same fingerprint, so an old verdict still holds.
  const fingerprint = crypto
    .createHash("sha256")
    .update(JSON.stringify({ id, requirement: requirement.text, template, focus: input.focus ?? "", probe }))
    .digest("hex")
    .slice(0, 16);
  fs.writeFileSync(path.join(dir, "fingerprint"), fingerprint);

  const brief = [
    `# ${id} – review (${input.pack})`,
    "",
    `**Requirement:** ${requirement.text}`,
    "",
    input.focus ? `**Look for:** ${input.focus}\n` : "",
    template.trim(),
    "",
    "## Evidence",
    "",
    ...shots.map((name) => `- ![${name}](${name})`),
    "- `probe.json`: measured values" + (meshes ? " and the building's meshes" : ""),
    input.log ? "- `log.txt`: the mod's log during the test" : "",
    "",
    `Fingerprint: \`${fingerprint}\``,
    "",
  ]
    .filter((line, i, all) => !(line === "" && all[i - 1] === ""))
    .join("\n");
  fs.writeFileSync(path.join(dir, "brief.md"), brief);
  return dir;
}

/** Spec files that write a pack for a requirement: `pack("ID", …)` with the ID as a literal. */
export function packedIds(dir = path.join(testsDir, "specs")): Set<string> {
  const ids = new Set<string>();
  const walk = (folder: string) => {
    for (const entry of fs.readdirSync(folder, { withFileTypes: true })) {
      const full = path.join(folder, entry.name);
      if (entry.isDirectory()) walk(full);
      else if (entry.name.endsWith(".spec.ts"))
        for (const match of fs.readFileSync(full, "utf8").matchAll(/\bpack\(\s*"([A-Z]+-\d{3})"/g)) ids.add(match[1]);
    }
  };
  walk(dir);
  return ids;
}
