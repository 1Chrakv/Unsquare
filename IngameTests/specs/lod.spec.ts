import { describe, expect, it } from "vitest";
import { bridge } from "../src/bridge.js";
import { Face, sameBounds } from "../src/expect.js";
import { building } from "../src/fixture.js";
import { move, target, useCity } from "../src/setup.js";

describe("LOD – levels of detail", () => {
  useCity();

  it("LOD-001 every LOD mesh of a deformed building is deformed", async (context) => {
    const house = await building(context, "plainHouse");
    const plain = (await bridge.meshes(house)).meshes;
    await target(house);
    await move("Face", Face.top, [1.5, 0, 0]); // a lean without twist keeps every LOD
    await bridge.frames(5);
    const deformed = await bridge.meshes(house);
    expect(deformed.prefab).toMatch(/__uq/);
    expect(deformed.meshes).toHaveLength(plain.length);
    deformed.meshes.forEach((mesh, i) => {
      expect(mesh.name, "a baked mesh").not.toBe(plain[i].name);
      expect(sameBounds([mesh], [plain[i]]), `mesh ${i} is deformed`).toBe(false);
      expect(mesh.lods, `LODs of mesh ${i}`).toHaveLength(plain[i].lods.length);
      mesh.lods.forEach((lod, j) => {
        expect(lod.name, `LOD ${j} is baked`).not.toBe(plain[i].lods[j].name);
        expect(sameBounds([lod], [plain[i].lods[j]]), `LOD ${j} of mesh ${i} is deformed`).toBe(false);
      });
    });
  });
});
