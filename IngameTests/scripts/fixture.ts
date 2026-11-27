// Fixture city tools.
//   check            loads the fixture save and reports which named buildings are recorded and found
//   record <name>    records the building currently selected in Unsquare mode under <name> (see Fixture.md)
//   record --list    lists the names
import { bridge } from "../src/bridge.js";
import { fixtureBuildings, readFixture, writeFixture, type FixtureName } from "../src/fixture.js";
import { game } from "../src/game.js";

const [command, name] = process.argv.slice(2);
const names = Object.keys(fixtureBuildings) as FixtureName[];

if (command === "record") {
  if (!name || name === "--list" || !names.includes(name as FixtureName)) {
    for (const n of names) console.log(`${n.padEnd(16)} ${fixtureBuildings[n].what}`);
    process.exit(name && name !== "--list" ? 2 : 0);
  }
  const session = await bridge.session();
  if (!session.target) {
    console.error("Select the building in Unsquare mode first (click it, no handle needed).");
    process.exit(1);
  }
  const fixture = readFixture();
  fixture.buildings[name as FixtureName] = session.target.ref;
  writeFixture(fixture);
  console.log(`${name} = ${session.target.ref}`);
} else if (command === "check") {
  await game.ensureRunning();
  const fixture = readFixture();
  const state = (await bridge.state()).result;
  if (state.save !== fixture.save) await bridge.load(fixture.save);
  let missing = 0;
  for (const n of names) {
    const ref = fixture.buildings[n];
    let status = "not recorded";
    if (ref) {
      try {
        const found = await bridge.building(ref);
        status = found.deformable ? "ok" : "found, but not deformable";
      } catch (error) {
        status = `not found (${(error as Error).message})`;
      }
    }
    if (status !== "ok") missing++;
    console.log(`${n.padEnd(16)} ${status.padEnd(28)} ${fixtureBuildings[n].what}`);
  }
  console.log(missing === 0 ? "\nFixture complete." : `\n${missing} of ${names.length} fixture buildings missing.`);
} else {
  console.error("Usage: tsx scripts/fixture.ts check | record <name> | record --list");
  process.exit(2);
}
