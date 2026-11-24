import { execFileSync } from "node:child_process";
import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";
import { bridge } from "../src/bridge.js";
import { game } from "../src/game.js";
import { deployedMod, modLog, repoDir } from "../src/paths.js";
import { skip } from "../src/fixture.js";

describe("ENV – test environment", () => {
  it("ENV-001 the running build is the commit under test", async () => {
    await game.ensureRunning();
    const { version, configuration } = (await bridge.ping()).result;
    expect(configuration).toBe("Debug");
    // The deployed commit's mod sources must equal the working tree's; tests and docs may differ.
    const deployed = /\+([0-9a-f]{7})/.exec(version)?.[1];
    expect(deployed, `version ${version} names its commit`).toBeTruthy();
    const changed = execFileSync("git", ["diff", "--name-only", deployed!, "--", "Unsquare", "UiModule", ":!*.md"], {
      cwd: repoDir,
      encoding: "utf8",
    }).trim();
    expect(changed, "mod sources changed since the deployed build: run dotnet build Unsquare.sln").toBe("");
    expect(fs.readFileSync(modLog, "utf8").split(/\r?\n/)[0]).toContain(`Unsquare ${version} (Debug, built`);
  });

  it("ENV-002 the deployed mod has its UI module", () => {
    for (const name of ["Unsquare.dll", "Unsquare.mjs", "Unsquare.css", "images"])
      expect(fs.existsSync(path.join(deployedMod, name)), `${name} in ${deployedMod}`).toBe(true);
  });

  it("ENV-003 a Release build contains no automation bridge", (context) => {
    const dll = path.join(repoDir, "Unsquare", "obj", "Release", "net48", "Unsquare.dll");
    if (!fs.existsSync(dll)) skip(context, "no Release build (dotnet msbuild Unsquare/Unsquare.csproj -t:Compile -p:Configuration=Release)");
    let types: string;
    try {
      types = execFileSync("ilspycmd", ["-l", "c", dll], { encoding: "utf8" });
    } catch {
      return skip(context, "ilspycmd not installed (dotnet tool install -g ilspycmd)");
    }
    expect(types).toContain("Unsquare.Mod");
    expect(types).not.toMatch(/Unsquare\.Automation\./);
  });
});
