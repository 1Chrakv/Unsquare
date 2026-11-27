import { describe, expect, it } from "vitest";
import { bridge } from "../src/bridge.js";
import { expectApplied, Face, offsets } from "../src/expect.js";
import { building } from "../src/fixture.js";
import { LogWatch } from "../src/log.js";
import { pack } from "../src/review.js";
import { bakes, target, useCity } from "../src/setup.js";
import { lookAt, shot } from "../src/view.js";

const Top = [2, 3, 6, 7];

describe("NUDGE – hotkey nudges", () => {
  useCity();

  it("NUDGE-001 four nudges of the top face pend, then bake once", async (context) => {
    const house = await building(context, "plainHouse");
    await target(house, "Face", Face.top);
    await lookAt(house, "front");
    const log = LogWatch.fromNow();
    let reply = await bridge.nudge([0, 1, 0]);
    expect(reply.session?.cage?.status).toBe("Pending");
    for (let i = 0; i < 3; i++) reply = await bridge.nudge([0, 1, 0]);
    expect(reply.result.result).toBe("Accepted");
    const pending = await shot("NUDGE-001-pending", { crop: "target" });

    const cage = await bridge.settled();
    expectApplied(cage);
    const corners = offsets(cage.targetOffsets);
    for (const corner of Top) expect(corners[corner][1], `corner ${corner} raised`).toBeCloseTo(0.4, 5);
    for (const corner of [0, 1, 4, 5]) expect(corners[corner]).toEqual([0, 0, 0]);
    expect(bakes(log), "one bake after the last nudge (rebake delay)").toHaveLength(1);
    await pack("NUDGE-001", {
      pack: "overlay",
      shots: { pending, applied: await shot("NUDGE-001-applied", { crop: "target" }) },
      probe: { status: cage.status, targetOffsets: cage.targetOffsets },
      building: house,
      focus: "The first shot (taken right after the nudges, maybe already baked) shows the cage raised 0.4 m above the roof; the second shows the roof risen to the cage.",
      log,
    });
  });

  it("NUDGE-002 the step cycles through the three sizes", async (context) => {
    await target(await building(context, "plainHouse"));
    const steps = [(await bridge.session()).step];
    for (let i = 0; i < 3; i++) steps.push(Number((await bridge.cycleStep()).result.result));
    expect(steps).toEqual([0.1, 0.5, 2, 0.1]);
  });

  it("NUDGE-003 the whole cage moves one step and Alt+R puts every corner back", async (context) => {
    await target(await building(context, "plainHouse"), "Cage");
    await bridge.nudge([1, 0, 0]);
    let cage = await bridge.settled();
    for (const corner of offsets(cage.targetOffsets)) expect(corner).toEqual([0.1, 0, 0]);

    await bridge.resetHandle();
    cage = await bridge.settled();
    expect(cage.deformed).toBe(false);
  });

  it("NUDGE-004 pushing a face through the opposite face is rejected and changes nothing", async (context) => {
    await target(await building(context, "plainHouse"), "Face", Face.top);
    await bridge.cycleStep();
    await bridge.cycleStep(); // 2 m steps
    const log = LogWatch.fromNow();
    let before = "";
    let result = "Accepted";
    for (let i = 0; i < 40 && result !== "Rejected"; i++) {
      before = (await bridge.session()).cage!.targetOffsets;
      const reply = await bridge.raw<{ result: string }>("POST", "session/nudge", { direction: [0, -1, 0] });
      result = reply.result.result;
    }
    expect(result).toBe("Rejected");
    expect((await bridge.session()).cage!.targetOffsets, "shape unchanged by the rejected nudge").toBe(before);
    expect(log.matching(/\[Edit\] Nudge Face3 .* -> Rejected/)).toHaveLength(1);
  });

  it("NUDGE-005 pushing a handle far beyond the box is clamped", async (context) => {
    await target(await building(context, "plainHouse"), "Corner", 7);
    await bridge.cycleStep();
    await bridge.cycleStep(); // 2 m steps
    const log = LogWatch.fromNow();
    let result = "Accepted";
    for (let i = 0; i < 60 && result === "Accepted"; i++)
      result = (await bridge.raw<{ result: string }>("POST", "session/nudge", { direction: [1, 0, 0] })).result.result;
    expect(result).toBe("Clamped");
    await log.waitFor(/\[Edit\] Nudge Corner7 .* -> Clamped/);
  });
});
