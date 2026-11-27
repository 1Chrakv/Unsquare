# Unsquare in-game tests

Automated tests for the requirements in [`Unsquare/Requirements.md`](../Unsquare/Requirements.md), run against a
live Cities: Skylines II. They talk to the mod's **automation bridge**, a REST endpoint that only Debug builds
contain (`Unsquare/Automation/`), and read `Unsquare.Mod.log`.

## Setup (once)

1. Node 20+, then `npm install` here.
2. Build the fixture city (see [Fixture.md](Fixture.md)).
3. `npm run bridge:enable` creates `ModsData\Unsquare\automation\enabled`; without it the bridge stays off.

## Running

| Command | What |
|---|---|
| `npm test` | Fast set (`specs/*.spec.ts`); starts the game if needed, loads the fixture once |
| `npm run test:slow` | Restarts, moved files, simulated days (`specs/slow/`) |
| `npm test -- -t UNDO-` | One requirement group (any Vitest filter) |
| `npm run coverage:req` | Which requirements have tests; checks every old checklist row is mapped |
| `npm run fixture:check` | What the fixture city lacks |
| `npm run game:start` / `game:quit` | Start or quit the game |

`UNSQUARE_SAVE=<save>` runs on another save (tests needing fixture buildings are then skipped, generic ones pick
buildings themselves). Results: `.testruns/<timestamp>/report.md` and `results.json` (also copied to
`.testruns/latest/`).

**Starting the game:** the suite starts `Cities2.exe` directly with `-developerMode -uiDeveloperMode`. A
`steam_appid.txt` (949230) next to the exe lets it skip Steam's relaunch and the Paradox Launcher; Steam must be
running. Builds (`dotnet build Unsquare.sln`) need the game closed: `npm run game:quit` first.

## Writing tests

- One spec file per requirement group, named in the group's heading in `Requirements.md`.
- Test names start with the requirement ID: `it("UNDO-002 a whole drag is undone in one step", …)`.
  `npm run coverage:req` rejects unknown IDs.
- `useCity()` loads the fixture and cleans up after every test (no target, nothing deformed, empty history,
  default modes). Tests must not save over the fixture (`bridge.save` only accepts `UnsquareTest-…` names other than it).
- Get buildings with `building(context, "<fixture name>")`: the test is skipped when the fixture lacks it.
- Wait with `bridge.settled()` (bake done, applied shape = requested), never with sleeps.
- Check the log with `LogWatch.fromNow()` before the step and `waitFor(/…/)` after it.
- Cage data is in building space: x width, y up, z towards the street. Corner index = x + 2y + 4z
  (`corner()` in `src/expect.ts`); faces -X, +X, -Y, +Y (top), -Z (back), +Z (street side).

## Bridge API

`src/bridge.ts` wraps every endpoint; all calls need the header `X-Unsquare-Token` (from
`ModsData\Unsquare\automation\token`, new at each start) and are refused with an `Origin` header. Replies are
`{ ok, result, error, frame, session }`; 409 means wrong game state, 422 a command the mod refused (its
`DeformRequestResult` in `result`). Calls that outlast `timeoutMs` answer 202 with a `/v1/jobs/{id}` URL to poll.
Unknown paths answer 404 with the list of routes.
