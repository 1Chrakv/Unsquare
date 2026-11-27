import os from "node:os";
import path from "node:path";
import { fileURLToPath } from "node:url";

/** Where the game, its user data and this repository live. Override with environment variables. */
export const userData =
  process.env.CSII_USERDATA ??
  path.join(os.homedir(), "AppData", "LocalLow", "Colossal Order", "Cities Skylines II");

export const gameDir =
  process.env.CSII_GAMEDIR ??
  (process.env.CSII_MANAGEDPATH
    ? path.resolve(process.env.CSII_MANAGEDPATH, "..", "..")
    : "C:\\Program Files (x86)\\Steam\\steamapps\\common\\Cities Skylines II");

export const gameExe = path.join(gameDir, "Cities2.exe");
export const modData = path.join(userData, "ModsData", "Unsquare");
export const automationDir = path.join(modData, "automation");
export const tokenFile = path.join(automationDir, "token");
export const modLog = path.join(userData, "Logs", "Unsquare.Mod.log");
export const playerLog = path.join(userData, "Player.log");
export const deployedMod = path.join(userData, "Mods", "Unsquare");
/** The player's saves, one folder per platform account. */
export const savesDir = path.join(userData, "Saves");
export const backupsDir = path.join(userData, "ModsData", "Unsquare-backups");

export const testsDir = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
export const repoDir = path.resolve(testsDir, "..");
export const requirementsFile = path.join(repoDir, "Unsquare", "Requirements.md");
export const fixtureFile = path.join(testsDir, "fixture.json");
export const runsDir = path.join(repoDir, ".testruns");
