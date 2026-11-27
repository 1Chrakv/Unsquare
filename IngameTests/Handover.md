# Handover: in-game test automation, phases 3–4

Phases 0–2 of the plan are done: requirements with IDs (`Unsquare/Requirements.md`), the Debug-only REST bridge
(`Unsquare/Automation/`), and this TypeScript suite with specs for the requirements that need no real input.
This file says what was learned, where things stand, and what comes next. How to run the suite: [README.md](README.md).

## Spike results (phase 0)

| Question | Result | Meaning for phases 3–4 |
|---|---|---|
| Start the game without the Paradox Launcher? | Yes. `Cities2.exe -developerMode -uiDeveloperMode` with `steam_appid.txt` (949230) next to the exe starts directly; Steam must be running. Without the file Steam relaunches the game through its "custom arguments" dialog and the launcher, and a direct start ends in a broken process ("platform service integration failed"). | `game.ts` writes the file itself. Fully unattended runs work. |
| `GameManager.Load` by save name? | Yes (`MapMetadataSystem` + `GameModeSystem.overrideMode`, then `Load` through `TaskManager`). A mid-size save takes about 110 s. | Load once per run (`loadFixture`), reset after each test instead of reloading. |
| Does the bridge answer while the window is unfocused or in the background? | Yes; the main thread keeps ticking. Not tried minimised. | Keep the window un-minimised to be safe. |
| Does it answer at the main menu? | Yes, from the `UIUpdate` pump. During boot requests queue until the first update. A load sent before the main menu is reached is overwritten by the game's own start-up: `state.ready` and a 409 on `game/load` guard that. | Always `waitReady()` before loading. |
| Edit cycle through the session? | Yes: select, handle, nudge (settled in about 0.6 s), undo, drag over frames, cancelled drag, `game/save` (2–3 s), `game/quit`. | — |
| InputSystem events reach the game? | **Not spiked.** | First task of phase 3 (see below). |
| `ScreenCapture` includes overlay and UI? | **Not spiked.** | Decides whether review packs need the overlay drawn by the game or a separate capture path. |
| cohtml inspector on port 9444 with `-uiDeveloperMode`? | **Not spiked.** | If it answers, Claude reads the game UI as DOM in the in-app browser instead of from screenshots. |

## State

Last full fast run: run 10 (2026-11-26) on `UnsquareTest-Fixture`: **94 passed, 7 failed, 2 skipped** of 103 tests,
about 16 minutes. Two fixes afterwards (skip clean-up, locale) were rerun on their own: SPLIT-014 and ENV-001 pass.

- **Coverage** (`npm run coverage:req`): 217 requirements; 205 for the suite, 12 Claude charters. **121** of the 205
  have tests (94 before run 10), and every old checklist row is mapped.
- **Failing fast tests, all filed in `Issues/`** (they stay red until the fix):
  - RESET-004: Reset all leaves props where the variant had them (issue 005).
  - BLEND-001 and ALIGN-011: in Building mode, ground areas move with the building (issue 007; align moved one
    pavement area 9.5 m).
  - DRAG-010: utility connection nodes stay behind on a whole-cage move (issue 006).
  - UPGRADE-007: on the raised car-park cage a nudge can do nothing and undo skips a step (issue 004).
  - UPGRADE-014: after a language switch deformed buildings keep their English names (issue 008).
- **Skipped (fixture gaps, see Fixture.md):** SPLIT-011 and SPLIT-015 check the storey and then skip the flat
  ground extension part; ALIGN-011 doesn't cover the park neighbour.
- **Open issues:** 002 (probably a symptom of 005), 004–009. 001 is closed (see below).
- **Slow set** (`npm run test:slow`, specs in `specs/slow/`), runs of 2026-11-26/27: **17 of 18 pass**.
  - Pass: `restart` (TERRAIN-010, SPLIT-010, SPLIT-012, DRAG-014), `cache` CACHE-002 and BAKE-007, `bake` (BAKE-004,
    BAKE-008), `levelup` (LEVELUP-001 to 003), `options` (OPTIONS-004), `save` (SAVE-001, SAVE-007, SAVE-004), `zoning`
    (ZONING-001, ZONING-002 with a quarter day).
  - CACHE-001 fails on a mod bug: housekeeping logs the eviction but no file leaves the disk (issue 009).
  - The test fixes on the way: a reset kept-alive socket after the long synchronous asset copy (`bridge.ts` retries
    once), tests that started without Unsquare mode after another test's load, level-ups followed by entity ref (a
    `Prefab@x,z` ref stops resolving when the prefab changes), and missing timeouts in `save.spec.ts`.
- **Issue 001 closed:** the raw `Deleted` shortcut crashes on `UnsquareTest-Spike`'s `FireHouse02` whether the
  station is plain, deformed or deformed-then-reset, and never on the fixture's `FireHouse01`; so the shortcut, not
  Unsquare, causes it. The player's case (the game's bulldozer on a deformed building) is UNDO-004, phase 3.
- **Without tests:** 84 suite requirements: input-driven ones (phase 3), `suite+review` ones (review packs), other
  mods and old saves (phase 4: FLEXIT, PLACEMAKER, COPY, LEGACY, SPLIT-016, SAVE-005/006, CACHE-003, ZONING-003/004).

## Still open from steps 1–3

Nothing: steps 1–3 are done. CACHE-001 stays red until issue 009 is fixed.

## Next steps (in order)

Issue fixes are not part of this work: each confirmed issue gets its own session and `fix/…` branch off `develop`
(005 Reset all first, it also simplifies the test clean-up; then 007, 004, 006, 009, 008; afterwards rerun DRAG-010 to close
002), and its requirement's test turns green there.

1. **Phase 3 spikes** with a throw-away Debug build: synthetic InputSystem events (`InputSystem.QueueStateEvent`
   on `Mouse.current`/`Keyboard.current`, or the game's `InputManager`), `ScreenCapture.CaptureScreenshotAsTexture`
   with and without UI, and `http://127.0.0.1:9444` in the in-app browser.
2. **View commands** (`Automation/Commands/ViewCommands.cs`): `POST view/camera {focus: ref, yaw, pitch, distance}`
   (the game's `CameraUpdateSystem`/orbit camera controller), `view/ui {visible}`, `view/screenshot {name, crop}`
   writing a JPEG to `.testruns/<run>/shots/`, and `GET view/screenPoint {handle | gizmo}` (world → screen with
   `Camera.main`).
3. **Input commands** (`InputCommands.cs`): `input/key {binding}` (through the mod's `CageHotkeys` table or real key
   events), `input/mouse {move | press | release | click, at: point | handle}`, `ui/trigger {binding, arg}` (calls the
   `UnsquareUISystem` trigger directly). Then specs for MODE, SEL, PICK, GIZMO, DRAG-004 to 008, KEYS, UNDO-004 (the
   game's bulldozer), UPGRADE-006, OPTIONS-005/006 and UI-005/006.
4. **Review packs** (`src/review.ts`): a spec calls `review.pack(id, { shots, probe, brief })` after its asserts; the
   pack goes to `.testruns/<run>/review/<ID>/` (`brief.md`, `before.jpg`/`after.jpg`, `probe.json`, `log.txt`).
   Packs: mesh, overlay, toolwindow, ground, foreign (see the Check column in Requirements.md).
5. **MCP shim** (`mcp/server.ts`, stdio, registered in the repo's `.mcp.json`): tools mirror `bridge.ts` groups,
   plus `screenshot`/`look_at` returning a downscaled JPEG inline, and `log {since}`.
6. **Skills** in `.claude/skills/`: `/ingame-review` (judges packs, verdicts keyed by ID + fingerprint of probe JSON
   and variant mesh hash, reused when unchanged), `/ingame-triage <test>` (replays a red test through MCP),
   `/ingame-explore <charter>`.
7. **Phase 4:** charters for the 12 `claude:` requirements (options, naming, placement, manifest) written into
   Requirements.md; `needs-mod` specs for Placemaker, FlexIt and Plop the Growables (ZONING-003) on their own saves,
   skipped when the mod is absent; `needs-save` specs on the user's old saves (LEGACY, SPLIT-016, SAVE-005,
   CACHE-003, ZONING-004); SAVE-006 needs a build with a raised `<Version>`.

## Open questions and known problems

- **One run at a time:** two runs on one game reset each other's buildings and selections (runs 3 and 4 overlapped
  after a crash, run 4's results were garbage). `scripts/run.ts` now holds `.testruns/run.lock` with its pid.
  A run whose game crashes restarts the game in the next spec file and goes on; stop it before starting another.
- **No raw entity changes:** adding `Deleted` to a building directly crashes the game natively, also on a plain
  building (closed issue 001). Test commands that change the city must go through the game's own tools, the
  session's commands, or data the game's systems then act on.
- **Autosaves:** with the game's autosave on (here every minute, keeping one), a city the tests let run autosaves,
  and the rotation deletes the player's own autosave. `scripts/run.ts` copies the player's autosaves aside before a
  run, puts back missing ones afterwards and moves the run's autosaves to `ModsData\Unsquare-backups\<stamp>-autosaves`.
  Scratch scripts run outside it: keep the city paused there.
- **ENV-001** fails while mod sources differ from the commit named in the deployed version: commit, then deploy,
  then run.
- **Simulation speed:** `UPGRADE-*` run the simulation at speed 3 for a number of frames; with the window in the
  background a frame takes longer, so these tests take about a minute each. Nothing hangs, they are just slow.

- **Tuned timings:** `game/load` settles 60 frames after the game says loading is done; terrain specs wait 30
  frames after a lift (`terrainSettles`) because the terrain pass runs a few frames after the swap; `move()` drags
  over 3 frames. Raise them before suspecting the mod when a test is flaky.
- **Terrain samples** stay in the lot interior (20–80 %): the edge strip blends to roads and neighbours by design.
- **ENV-003** needs a Release build (`dotnet msbuild Unsquare/Unsquare.csproj -t:Compile -p:Configuration=Release`)
  and `ilspycmd`; otherwise it is skipped.
- **SAVE-004** (without Unsquare) moves the deployed mod folder aside and starts the game with
  `-startGame=<guid>` (the save's metadata guid, which `game/save` returns; note the `=`). `-continuelastsave` only
  asks the main menu, which can stop at a prompt. The start is found in `SceneFlow.log` by that guid: Windows doesn't
  update the modified time of a log the game holds open. The city runs unpaused, so the test kills the game 10 s after
  the load, before its first autosave. The game skips the unknown `SavedCage` through `Debug.LogWarning`, which this
  build logs nowhere. If the run is interrupted, rename `Mods\Unsquare.disabled-by-tests` back by hand.
- **Slow specs** back up `ModsData\Unsquare` (`ModDataBackup`) and move manifest files aside, never delete them.
  Backups are in `ModsData\Unsquare-backups\`.
- **Steam must run** for a direct start, or the game comes up broken (no mods, endless `OnGUI` errors) and never
  answers. `game.start()` starts Steam itself now.
- **A test skipped after it changed the city gets no afterEach clean-up**: use `skipAfterChanges`, not `skip`.
- **Simulated time:** the game runs at most 8 frames per rendered frame, so an in-game day takes 10–20 minutes even
  at full speed. ZONING-001 runs a quarter day (`UNSQUARE_DAYS` for more); "a few days" is not covered yet.
- **Test-only city commands** change data the game's own systems then act on: `zone/paint` writes zone cells and
  marks their blocks `Updated` (like the zone tool's apply); `levelUp` raises the building's condition and marks its
  level-up materials delivered, then the game levels it up itself.
- No CI: the suite needs a desktop with the game. Runs are local, on the user's OK per run.
