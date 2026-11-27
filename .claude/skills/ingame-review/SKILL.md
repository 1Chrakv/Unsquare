---
name: ingame-review
description: Judge the in-game suite's review packs (screenshots plus measurements in .testruns/<run>/review/<ID>/) and write pass/fail verdicts to .testruns/verdicts.json, reusing verdicts whose fingerprint is unchanged. Use after a test run that wrote review packs, or when the user asks to review screenshots of Unsquare requirements.
---

# /ingame-review [--all | ID …]

Judges what the in-game suite cannot: how a deformed mesh, the cage overlay, the tool window or a ground
layout looks. The suite has already asserted every measurable part; you only judge the picture against the
requirement.

## Steps

1. Find the run: `.testruns/latest/review/` (or the run the user names). Each folder `<ID>/` holds
   `brief.md`, the screenshots, `probe.json`, maybe `log.txt`, and `fingerprint`.
2. Pick the packs: the IDs given, or with `--all` every pack. Without arguments, every pack whose
   fingerprint differs from its entry in `.testruns/verdicts.json` (or has none).
3. **Reuse first.** If `.testruns/verdicts.json` has the ID with the same fingerprint, keep that verdict and do
   not open the images. Say how many verdicts were reused.
4. For each remaining pack:
   - Read `brief.md` (the requirement, the pack's checklist, what to look for) and `probe.json`.
   - Look at each screenshot listed in the brief (Read tool on the image file).
   - Only if something is unclear and the game is running with the city still in that state, take at most two
     more pictures with the `unsquare-game` MCP tools (`look_at`, `screenshot`). Never change the city or load
     a save for a review.
   - Decide **pass** or **fail** against the brief's checklist. Fail only for what the requirement asks and
     the picture clearly shows; when the evidence can't show it, say "fail: evidence insufficient" and name
     the missing view.
5. Write the verdicts into `.testruns/verdicts.json` (an object keyed by ID; keep other entries):
   `{"<ID>": {"fingerprint": "<from the pack>", "verdict": "pass" | "fail", "reason": "<one sentence>",
   "evidence": "<pack folder>/<image>", "date": "<ISO date>"}}`.
6. Re-run the report so it shows the verdicts:
   `cd IngameTests && npx tsx -e "import('./src/report.ts').then(m => m.writeReport('../.testruns/latest/results.json', '../.testruns/latest/report.md', 'review'))"`
   (skip it if the run has no `results.json`).
7. Reply with a short table: ID, verdict, reason. For each fail, offer to file an issue (`Issues/README.md`
   workflow); don't file it unasked.

## Rules

- Judge only; never change code, settings or the city.
- One sentence per reason, naming what is wrong and where ("roof edge torn at the front left corner").
- Small aliasing, LOD pops, shadows and lighting changes from the time of day are no failure.
