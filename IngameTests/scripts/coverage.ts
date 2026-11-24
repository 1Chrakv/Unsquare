// Requirement coverage: every old checklist row is mapped, every test names a known requirement, and which
// requirements still lack a test or charter. `--strict` fails while any suite requirement has no test.
import { checkKind, oldRows, readRequirements, testedIds } from "../src/requirements.js";

const OldChecklistRows = 219;
const strict = process.argv.includes("--strict");

const requirements = readRequirements();
const tested = testedIds();
const known = new Set(requirements.map((r) => r.id));
let failed = false;

const mapped = new Set(requirements.flatMap(oldRows));
const unmapped = Array.from({ length: OldChecklistRows }, (_, i) => i + 1).filter((row) => !mapped.has(row));
if (unmapped.length > 0) {
  console.error(`Old checklist rows without a requirement: ${unmapped.join(", ")}`);
  failed = true;
}

const unknown = [...tested.keys()].filter((id) => !known.has(id));
if (unknown.length > 0) {
  console.error(`Tests name unknown requirements: ${unknown.map((id) => `${id} (${tested.get(id)!.join(", ")})`).join("; ")}`);
  failed = true;
}

const missing: Record<string, string[]> = { suite: [], "suite-slow": [], review: [], claude: [] };
for (const requirement of requirements) {
  const kind = checkKind(requirement);
  if (kind !== "claude" && !tested.has(requirement.id)) missing[kind].push(requirement.id);
}

const testable = requirements.filter((r) => checkKind(r) !== "claude");
console.log(`Requirements: ${requirements.length} (${testable.length} with tests, ${requirements.length - testable.length} Claude charters)`);
console.log(`Tested: ${testable.filter((r) => tested.has(r.id)).length} of ${testable.length}`);
console.log(`Old rows mapped: ${mapped.size >= OldChecklistRows ? "all" : `${mapped.size}`} of ${OldChecklistRows}`);
for (const [kind, ids] of Object.entries(missing))
  if (ids.length > 0) console.log(`Without test (${kind}, ${ids.length}): ${ids.join(" ")}`);

if (strict && Object.values(missing).some((ids) => ids.length > 0)) failed = true;
process.exit(failed ? 1 : 0);
