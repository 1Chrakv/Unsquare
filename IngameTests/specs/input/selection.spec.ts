import { describe, expect, it } from "vitest";
import { bridge } from "../../src/bridge.js";
import { corner, Face } from "../../src/expect.js";
import { building } from "../../src/fixture.js";
import { centreOf, click, groundBefore, hover, pointOf, useInputCity } from "../../src/input.js";
import { LogWatch } from "../../src/log.js";
import { target } from "../../src/setup.js";
import { lookAt } from "../../src/view.js";

const targetId = async () => (await bridge.session()).target?.id ?? null;
const idOf = async (ref: string) => (await bridge.building(ref)).id;

describe("SEL – selecting with the mouse", () => {
  useInputCity();

  it("SEL-001 a click targets a building, another click switches, empty ground changes nothing", async (context) => {
    const house = await building(context, "plainHouse");
    const other = await building(context, "storefront");
    await lookAt(house, "front", 160);
    const log = LogWatch.fromNow();
    await click(await centreOf(house));
    expect(await targetId()).toBe(await idOf(house));
    await click(await centreOf(other));
    expect(await targetId(), "switched").toBe(await idOf(other));
    expect(log.matching(/\[Edit\] Target /)).toHaveLength(2);
    // Close to the cage: clicks farther out step back (SEL-005).
    await click(await groundBefore(other, 0.8));
    expect(await targetId(), "empty ground next to it changes nothing").toBe(await idOf(other));
  });

  it("SEL-002 the first click selects no handle, a click on a handle selects it with its gizmo", async (context) => {
    const house = await building(context, "plainHouse");
    await lookAt(house, "front", 110);
    await bridge.pick("Corner");
    await click(await centreOf(house));
    expect((await bridge.session()).handle, "the first click only targets").toBeNull();
    const log = LogWatch.fromNow();
    await click({ handle: `Corner${corner(1, 1, 1)}` });
    expect((await bridge.session()).handle).toBe("Corner7");
    await log.waitFor(/\[Edit\] Selected Corner7/);
    expect((await bridge.screenPoint({ gizmo: "arrowY" })).onScreen, "the gizmo is there").toBe(true);
  });

  it("SEL-005 clicks near the cage keep the selection, far clicks step back handle and then target", async (context) => {
    const house = await building(context, "plainHouse");
    await target(house, "Face", Face.top);
    await lookAt(house, "front", 160);
    await click(await groundBefore(house, 0.8));
    expect((await bridge.session()).handle, "a click just outside the cage keeps the handle").toBe("Face3");
    const far = await groundBefore(house, 10); // well beyond the 5 % margin of the 32 m cage
    await click(far);
    let session = await bridge.session();
    expect(session.handle, "the first far click drops the handle").toBeNull();
    expect(session.target?.id).toBe(await idOf(house));
    await click(far);
    session = await bridge.session();
    expect(session.target, "the second far click drops the target").toBeNull();
    expect(session.mode).toBe(true);
  });

  it("SEL-006 hovering previews a cage only without a selected handle and not over the ground", async (context) => {
    const house = await building(context, "plainHouse");
    const other = await building(context, "storefront");
    await lookAt(house, "front", 160);
    await hover(await centreOf(house));
    expect((await bridge.session()).preview, "hovered building previews").toBe(await idOf(house));
    await hover(await groundBefore(house));
    expect((await bridge.session()).preview, "no preview over the ground").toBeNull();
    await target(house, "Face", Face.top);
    await hover(await centreOf(other));
    expect((await bridge.session()).preview, "no preview while a handle is selected").toBeNull();
  });

  it("SEL-007 a park is picked from its lot and a garden click picks its house", async (context) => {
    const park = await building(context, "park");
    await lookAt(park, "top", 150);
    await click(await pointOf(park, (_, lot) => [(lot.min[0] + lot.max[0]) / 2, 0.2, (lot.min[2] + lot.max[2]) / 2]));
    expect(await targetId(), "park picked from its lot").toBe(await idOf(park));
    await bridge.stepBack();

    const house = await building(context, "gardenHouse");
    await lookAt(house, "top", 110);
    await click(await pointOf(house, (_, lot) => [lot.min[0] + 2, 0.2, lot.max[2] - 2]));
    expect(await targetId(), "the garden picks the house").toBe(await idOf(house));
  });

  it("SEL-008 a school wing is picked on its own; the car park's storey picks the car park", async (context) => {
    const school = await building(context, "school");
    const wings = (await bridge.info(school)).upgrades ?? [];
    const wing = wings.find((u) => /Extension|Wing|Classroom/i.test(u.prefab ?? ""));
    if (!wing) return context.skip();
    const wingRef = (await bridge.building(wing.id)).ref;
    await lookAt(school, "top", 160);
    expect(await hover(await centreOf(wingRef))).toBeDefined();
    expect((await bridge.session()).preview, "hovering the wing previews the wing").toBe(wing.id);
    await click(await centreOf(wingRef));
    expect(await targetId(), "the wing has its own cage").toBe(wing.id);
    await bridge.stepBack();
    await click(await centreOf(school));
    expect(await targetId(), "the school's own building picks the school").toBe(await idOf(school));
    await bridge.stepBack();

    const carPark = await building(context, "carPark");
    await lookAt(carPark, "front", 130);
    // Near the top of the raised box: the extra storey stands there.
    await click(await pointOf(carPark, (box) => [(box.min[0] + box.max[0]) / 2, box.max[1] - 1.5, (box.min[2] + box.max[2]) / 2]));
    expect(await targetId(), "the storey picks the car park").toBe(await idOf(carPark));
  });
});
