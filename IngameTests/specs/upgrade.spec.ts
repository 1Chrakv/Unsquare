import { describe, expect, it } from "vitest";
import { bridge } from "../src/bridge.js";
import { corner, offsets } from "../src/expect.js";
import { building, skip } from "../src/fixture.js";
import { LogWatch } from "../src/log.js";
import { expectConnected, move, ReloadTimeout, saveAndReload, target, useCity } from "../src/setup.js";

/** Lets the simulation run for about `seconds` of real time at full speed, then pauses it again. */
async function simulate(seconds: number) {
  await bridge.speed(3);
  await bridge.frames(seconds * 30);
  await bridge.speed(0);
}

describe("UPGRADE – upgrades and extensions", () => {
  useCity();

  it("UPGRADE-001 a car park's storey bends with it and its nets stay connected", async (context) => {
    const carPark = await building(context, "carPark");
    expect((await bridge.info(carPark)).upgrades.length).toBeGreaterThan(0);
    await target(carPark);
    const log = LogWatch.fromNow();
    await move("Corner", corner(1, 1, 1), [2, 0, 1]);
    // Logged once per building and session, so look at the whole log.
    const entity = (await bridge.building(carPark)).id.replace(/^#(\d+):(\d+)$/, "Entity($1:$2)");
    expect(LogWatch.fromStart().matching(/Upgrades of building/).some((line) => line.includes(entity))).toBe(true);
    await log.waitFor(/Lot sync for building .*, \d+ extensions/, 20_000);
    await expectConnected(carPark);
  });

  it("UPGRADE-002 resetting and undoing a deformed car park after some simulation doesn't crash", async (context) => {
    const carPark = await building(context, "carPark");
    await target(carPark);
    await move("Corner", corner(1, 1, 1), [2, 0, 1]);
    await simulate(20);
    await bridge.reset();
    expect((await bridge.settled()).deformed).toBe(false);
    await bridge.undo();
    expect((await bridge.settled()).deformed).toBe(true);
    await simulate(5);
    expect((await bridge.ping()).ok).toBe(true);
  });

  it("UPGRADE-004 a school with its wing survives simulation and reset; the wing stays plain", async (context) => {
    const school = await building(context, "school");
    const wing = (await bridge.info(school)).upgrades[0];
    await target(school);
    await move("Corner", corner(1, 1, 1), [1.5, 0, 0]);
    await simulate(20);
    await bridge.reset();
    expect((await bridge.settled()).deformed).toBe(false);
    if (wing) expect((await bridge.info(wing.id)).prefab).not.toMatch(/__uq/);
    await expectConnected(school);
  });

  it("UPGRADE-007 every 0.1 m nudge of a car park's top corner moves it; undo and redo restore each step", async (context) => {
    await target(await building(context, "carPark"), "Corner", corner(1, 1, 1));
    const heights: number[] = [];
    for (let i = 0; i < 3; i++) {
      await bridge.nudge([0, 1, 0]);
      heights.push(offsets((await bridge.settled()).targetOffsets)[7][1]);
    }
    expect(heights.map((h) => Math.round(h * 10))).toEqual([1, 2, 3]);
    for (let i = 2; i >= 0; i--) {
      await bridge.undo();
      expect(offsets((await bridge.settled()).targetOffsets)[7][1]).toBeCloseTo(i * 0.1, 5);
    }
    await bridge.redo();
    expect(offsets((await bridge.settled()).targetOffsets)[7][1]).toBeCloseTo(0.1, 5);
  });

  it("UPGRADE-003 the deformed storey survives save and reload; the upgrade menu offers no extra entries", async (context) => {
    const carPark = await building(context, "carPark");
    const options = (await bridge.info(carPark)).upgradeOptions;
    await target(carPark);
    await move("Corner", corner(1, 1, 1), [2, 0, 1]);
    const spec = (await bridge.settled()).targetSpec;
    expect((await bridge.info(carPark)).upgradeOptions, "options of the deformed car park").toEqual(options);
    await saveAndReload();
    expect((await bridge.settled(carPark)).targetSpec).toBe(spec);
    const after = await bridge.info(carPark);
    expect(after.upgrades.length).toBeGreaterThan(0);
    for (const upgrade of after.upgrades) expect(upgrade.prefab, "the storey is on its variant again").toMatch(/__uq/);
    expect(after.upgradeOptions, "options after the reload").toEqual(options);
  }, ReloadTimeout);

  it("UPGRADE-005 a school wing deformed alone keeps its paths; reset, undo and reload restore it", async (context) => {
    const school = await building(context, "school");
    const upgrades = (await bridge.info(school)).upgrades;
    const wing = upgrades.find((u) => /Classroom/i.test(u.prefab)) ?? upgrades[0];
    if (!wing) skip(context, "the school has no wing");
    const wingRef = (await bridge.building(wing.id)).ref; // survives the reload, unlike the entity id
    await target(wingRef);
    await move("Corner", corner(1, 1, 1), [1.5, 0, 0]);
    const spec = (await bridge.settled()).targetSpec;
    await expectConnected(wingRef);
    await expectConnected(school);
    expect((await bridge.info(school)).prefab, "the school stays plain").not.toMatch(/__uq/);
    await bridge.reset();
    expect((await bridge.settled()).deformed).toBe(false);
    await bridge.undo();
    expect((await bridge.settled()).targetSpec).toBe(spec);
    await saveAndReload();
    expect((await bridge.settled(wingRef)).targetSpec).toBe(spec);
    await expectConnected(wingRef);
  }, ReloadTimeout);

  it("UPGRADE-008 after a reload the car park's cage again reaches the storey's top", async (context) => {
    const carPark = await building(context, "carPark");
    await target(carPark);
    await move("Corner", corner(1, 1, 1), [1, 0, 0]);
    const cage = (await bridge.session()).cage!;
    expect(cage.box.max[1], "raised to the storey").toBeGreaterThan(cage.lotBox.max[1] + 1);
    await saveAndReload();
    await target(carPark);
    expect((await bridge.session()).cage!.box.max[1]).toBeCloseTo(cage.box.max[1], 1);
  }, ReloadTimeout);

  it("UPGRADE-014 after switching the game language and back, deformed buildings keep their names", async (context) => {
    const carPark = await building(context, "carPark");
    const plain = (await bridge.info(carPark)).name;
    await target(carPark);
    await move("Corner", corner(1, 1, 1), [1, 0, 0]);
    expect((await bridge.info(carPark)).name).toBe(plain);
    const reply = await bridge.raw<{ previous: string; supported: string[] }>("POST", "game/locale", { locale: "de-DE" });
    const previous = reply.result.previous;
    if (!reply.ok) {
      const other = reply.result.supported.find((id) => id !== previous && !id.startsWith("en"))!;
      await bridge.locale(other);
    }
    try {
      await bridge.frames(30);
      const german = (await bridge.info(carPark)).name;
      expect(german, "a German name").not.toBe(plain);
      expect(german).not.toMatch(/__uq|Assets\./);
    } finally {
      await bridge.locale(previous);
      await bridge.frames(30);
    }
    expect((await bridge.info(carPark)).name).toBe(plain);
  });
});
