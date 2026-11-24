import { describe, expect, it } from "vitest";
import { bridge } from "../src/bridge.js";
import { corner, offsets } from "../src/expect.js";
import { building } from "../src/fixture.js";
import { LogWatch } from "../src/log.js";
import { move, refused, target, useCity } from "../src/setup.js";

const isIdentity = (text: string) => offsets(text).every((c) => c.every((v) => v === 0));

/** Leans the building 1 m to +X in Building mode (top corners), which splits it from its lot. */
async function lean() {
  await bridge.editMode("Building");
  await bridge.handle("Face", 3);
  await bridge.drag({ translate: [1, 0, 0], frames: 4 });
  return bridge.settled();
}

describe("SPLIT – edit modes and split cages", () => {
  useCity();

  it("SPLIT-001 the edit mode switches and is logged", async (context) => {
    await target(await building(context, "plainHouse"));
    for (const mode of ["Building", "Lot", "Both"] as const) {
      const log = LogWatch.fromNow();
      await bridge.editMode(mode);
      expect((await bridge.session()).editMode).toBe(mode);
      await log.waitFor(new RegExp(`\\[Edit\\] Edit mode ${mode}`));
    }
  });

  it("SPLIT-002 Both mode keeps one linked cage; link is refused", async (context) => {
    await target(await building(context, "plainHouse"));
    await move("Corner", corner(1, 1, 1), [1, 1, 0]);
    const cage = (await bridge.session()).cage!;
    expect(cage.split).toBe(false);
    expect(cage.hasGhost).toBe(false);
    expect(await refused(bridge.link("lot"))).toBe("AlreadyLinked");
  });

  it("SPLIT-005 Lot mode moves the lot only; the building cage shows as ghost", async (context) => {
    await target(await building(context, "plainHouse"));
    await bridge.editMode("Lot");
    await move("Corner", corner(1, 0, 1), [1.5, 0, 0]);
    const lot = (await bridge.session()).cage!;
    expect(lot.split).toBe(true);
    expect(lot.hasGhost).toBe(true);
    await bridge.editMode("Building");
    expect(isIdentity((await bridge.session()).cage!.targetOffsets), "building unchanged").toBe(true);
  });

  it("SPLIT-006 a lifted lot leaves the building off its ground until Building-mode fit; terrain is the lot's", async (context) => {
    await target(await building(context, "plainHouse"));
    await bridge.editMode("Lot");
    await move("Corner", corner(0, 0, 0), [0, 2, 0]);
    await move("Corner", corner(1, 0, 0), [0, 2, 0]);
    await bridge.editMode("Building");
    expect((await bridge.session()).cage!.offGround).toBe(true);
    expect(await refused(bridge.toggleTerrain())).toBe("Rejected");
    expect((await bridge.session()).problem).toBe("TerrainOfLot");
    await bridge.fit();
    await bridge.frames(5);
    expect((await bridge.settled()).offGround).toBe(false);
  });

  it("SPLIT-007 a split building is carried by lot drags in Both mode; link makes it follow exactly", async (context) => {
    await target(await building(context, "plainHouse"));
    await lean();
    await bridge.editMode("Both");
    await move("Corner", corner(1, 0, 1), [1, 0, 0]);
    expect((await bridge.session()).cage!.split, "keeps its own lean").toBe(true);
    await bridge.link("lot");
    const linked = await bridge.settled();
    expect(linked.split).toBe(false);
    expect(linked.hasGhost).toBe(false);
  });

  it("SPLIT-008 reset in each mode resets only its cage; undo restores each step", async (context) => {
    await target(await building(context, "plainHouse"));
    await lean();
    await bridge.editMode("Lot");
    await move("Corner", corner(1, 0, 1), [1, 0, 0]);

    await bridge.editMode("Building");
    await bridge.reset();
    expect(isIdentity((await bridge.settled()).targetOffsets)).toBe(true);
    await bridge.editMode("Lot");
    expect(isIdentity((await bridge.session()).cage!.targetOffsets), "lot kept").toBe(false);
    await bridge.reset();
    expect((await bridge.settled()).deformed).toBe(false);

    await bridge.undo();
    await bridge.undo();
    await bridge.settled();
    await bridge.editMode("Building");
    expect(isIdentity((await bridge.session()).cage!.targetOffsets), "building lean back").toBe(false);
  });

  it("SPLIT-009 nudging the building cage up and down returns it exactly", async (context) => {
    await target(await building(context, "plainHouse"));
    await bridge.editMode("Building");
    await bridge.handle("Face", 3);
    await bridge.nudge([0, 1, 0]);
    await bridge.settled();
    await bridge.nudge([0, -1, 0]);
    const cage = await bridge.settled();
    expect(cage.deformed).toBe(false);
    expect(cage.split).toBe(false);
    await bridge.undo();
    expect((await bridge.settled()).deformed).toBe(true);
    await bridge.undo();
    expect((await bridge.settled()).deformed).toBe(false);
  });

  it("SPLIT-013 the lot follows the building on request; undo restores the split", async (context) => {
    await target(await building(context, "plainHouse"));
    const leaned = await lean();
    const log = LogWatch.fromNow();
    await bridge.link("building");
    const linked = await bridge.settled();
    expect(linked.split).toBe(false);
    expect(linked.targetOffsets).toBe(leaned.targetOffsets);
    await log.waitFor(/\[Edit\] Link lot to building -> Accepted/);
    await bridge.undo();
    expect((await bridge.settled()).split).toBe(true);
  });
});
