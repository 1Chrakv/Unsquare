---
name: ingame-triage
description: Triage a red in-game test of Unsquare - read its failure in the latest report, replay its steps through the unsquare-game MCP tools, read the mod log slice and state the likely cause (mod bug, test mistake, timing, fixture). Use when the user asks why an in-game test failed or to triage a failing requirement ID.
---

# /ingame-triage <requirement ID or test name>

Finds out why one test of `IngameTests/` failed, without fixing anything unless the user asks.

## Steps

1. **Read the failure.** In `.testruns/latest/report.md` find the test (by ID or name): its assertion message
   and the requirement text. Note the run's stamp.
2. **Read the test.** Find the spec with `Grep` for the ID under `IngameTests/specs/`. Note every bridge call it
   makes (`target`, `move`, `bridge.*`, `input.*`), the fixture building it uses (`IngameTests/fixture.json`)
   and its timeouts.
3. **Check known causes first** (`IngameTests/Handover.md`, "Open questions and known problems", and open
   files in `Issues/`): an issue already filed for this requirement, an overlapping test run (run lock), a game
   crash earlier in the run, deploy/commit mismatch (ENV-001), tuned timings.
4. **Replay** only if the game is running, no test run holds `.testruns/run.lock`, and the user agreed to use
   the game. Use the `unsquare-game` MCP tools: `game_state`, then the test's steps one by one (`session`,
   `input_mouse`, `input_key`, `wait_settled`, `probe`), comparing each result with what the test expects.
   Take a `look_at` screenshot where the failure is visual. Never load the user's own saves; use
   `UnsquareTest-Fixture` and save only as `UnsquareTest-*`.
5. **Read the log**: `log` with the test's marker (`[Auto] >>>` lines) or the last 100 lines; look for
   `[Edit] … ->` results, exceptions and `refused`.
6. **Conclude** in a few lines: the step that diverges, expected vs. actual, and the likely cause, one of:
   mod bug, test mistake, timing/flakiness, fixture gap, environment. Say how sure you are and what would confirm it.
7. **Offer the next step** instead of doing it: file an issue (`Issues/README.md` workflow) for a mod bug, or
   the test fix for a test mistake. Mod fixes go to their own `fix/…` branch off `develop`.

## Rules

- No code, settings or save changes without the user's go-ahead.
- Restore what you changed in the city during a replay (`session` resetAll, stepBack) before you finish.
- Ask before starting the game; the user may be using the PC.
