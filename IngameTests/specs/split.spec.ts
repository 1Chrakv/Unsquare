import { describe, expect, it } from "vitest";
import { bridge } from "../src/bridge.js";
import { corner, distanceXZ, Face, offsets, sameBounds } from "../src/expect.js";
import { building, readFixture, skip, skipAfterChanges } from "../src/fixture.js";
import { LogWatch } from "../src/log.js";
import { move, refused, ReloadTimeout, saveAndReload, target, useCity } from "../src/setup.js";

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

  it("SPLIT-011 leaning a car park in Building mode leans its storey", async (context) => {
    const carPark = await building(context, "carPark");
    const storey = (await bridge.info(carPark)).upgrades[0];
    if (!storey) skip(context, "the car park has no storey");
    const before = (await bridge.meshes(storey.id)).meshes;
    await target(carPark);
    const cage = await lean();
    expect(cage.lotDeformed, "the lot stays").toBe(false);
    await bridge.frames(10);
    expect((await bridge.info(storey.id)).prefab).toMatch(/__uq/);
    expect(sameBounds((await bridge.meshes(storey.id)).meshes, before), "the storey leans").toBe(false);
    if (!(await flatExtension())) await skipAfterChanges(context, "storey checked; no ground-level extension beside a building in the fixture");
  });

  it("SPLIT-014 a hospital's ward wing bends with the Building-mode cage; lot and paths stay; reload keeps it", async (context) => {
    const hospital = await building(context, "hospital");
    const ward = (await bridge.info(hospital)).upgrades.find((u) => /Ward/i.test(u.prefab));
    if (!ward) skip(context, "the hospital has no ward wing");
    const wardMeshes = (await bridge.meshes(ward.id)).meshes;
    const nodes = (await bridge.nets(hospital)).nodes;
    await target(hospital);
    await bridge.editMode("Building");
    const box = (await bridge.session()).cage!.box;
    for (const mesh of wardMeshes)
      for (const axis of [0, 2]) {
        expect(mesh.min[axis], "the wing is inside the Building-mode cage").toBeGreaterThanOrEqual(box.min[axis] - 0.05);
        expect(mesh.max[axis], "the wing is inside the Building-mode cage").toBeLessThanOrEqual(box.max[axis] + 0.05);
      }
    await move("Face", Face.top, [1.5, 0, 0]);
    const cage = await bridge.settled();
    expect(cage.lotDeformed, "the lot cage stays").toBe(false);
    await bridge.frames(10);
    expect((await bridge.info(ward.id)).prefab).toMatch(/__uq/);
    expect(sameBounds((await bridge.meshes(ward.id)).meshes, wardMeshes), "the wing bends").toBe(false);
    const after = (await bridge.nets(hospital)).nodes;
    nodes.forEach((node, i) => expect(distanceXZ(after[i].local, node.local), `path node ${node.id}`).toBeLessThan(0.05));

    await saveAndReload();
    expect((await bridge.settled(hospital)).targetSpec).toBe(cage.targetSpec);
    const wardAfter = (await bridge.info(hospital)).upgrades.find((u) => /Ward/i.test(u.prefab))!;
    expect(wardAfter.prefab).toMatch(/__uq/);
  }, ReloadTimeout);

  it("SPLIT-015 the Building-mode cage encloses a storey; flat pieces beside the building don't enlarge it", async (context) => {
    const carPark = await building(context, "carPark");
    const storey = (await bridge.info(carPark)).upgrades[0];
    if (!storey) skip(context, "the car park has no storey");
    const top = Math.max(...(await bridge.meshes(storey.id)).meshes.map((m) => m.max[1]));
    await target(carPark);
    await bridge.editMode("Building");
    const cage = (await bridge.session()).cage!;
    expect(top, "the storey rises above the lot box").toBeGreaterThan(cage.lotBox.max[1]);
    expect(cage.box.max[1], "the cage reaches the storey's top").toBeGreaterThanOrEqual(top - 0.05);
    const flat = await flatExtension();
    if (!flat) return skipAfterChanges(context, "storey checked; no ground-level extension beside a building in the fixture");
    await bridge.stepBack();
    await target(flat.owner);
    await bridge.editMode("Building");
    const box = (await bridge.session()).cage!.box;
    const outside = flat.meshes.some(
      (m) => m.min[0] > box.max[0] || m.max[0] < box.min[0] || m.min[2] > box.max[2] || m.max[2] < box.min[2],
    );
    expect(outside, "the flat piece stays outside the Building-mode cage").toBe(true);
    await move("Face", Face.top, [1, 0, 0]);
    await bridge.frames(10);
    expect(sameBounds((await bridge.meshes(flat.id)).meshes, flat.meshes), "a building lean leaves it").toBe(true);
  });
});

/** An on-lot extension of a fixture building that lies flat on the ground (car park lot, ground helipad). */
async function flatExtension() {
  for (const name of ["hospital", "carPark", "school"] as const) {
    const owner = readFixture().buildings[name];
    if (!owner) continue;
    for (const upgrade of (await bridge.info(owner)).upgrades) {
      const meshes = (await bridge.meshes(upgrade.id)).meshes;
      if (meshes.length > 0 && Math.max(...meshes.map((m) => m.max[1])) < 4) return { owner, id: upgrade.id, meshes };
    }
  }
  return undefined;
}
