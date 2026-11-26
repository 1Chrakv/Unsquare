import { execFileSync, spawn } from "node:child_process";
import fs from "node:fs";
import path from "node:path";
import { bridge, sleep } from "./bridge.js";
import { automationDir, backupsDir, gameDir, gameExe, modData, modLog } from "./paths.js";

/**
 * Starting, stopping and restarting Cities: Skylines II for the tests. The game is started directly (not through
 * Steam or the Paradox Launcher): a `steam_appid.txt` next to `Cities2.exe` lets it skip Steam's relaunch.
 */
export const game = {
  isRunning(): boolean {
    try {
      const out = execFileSync("tasklist", ["/NH", "/FI", "IMAGENAME eq Cities2.exe"], { encoding: "utf8" });
      return out.toLowerCase().includes("cities2.exe");
    } catch {
      return false;
    }
  },

  /** Creates the bridge's enable file (the bridge only exists in Debug builds). */
  enableBridge() {
    fs.mkdirSync(automationDir, { recursive: true });
    const enabled = path.join(automationDir, "enabled");
    if (!fs.existsSync(enabled)) fs.writeFileSync(enabled, "");
  },

  /** Starts the game and waits until the bridge answers in the main menu. */
  async start(timeoutMs = 400_000) {
    if (this.isRunning()) throw new Error("The game is already running.");
    this.enableBridge();
    await ensureSteam();
    const started = Date.now();
    this.launch();
    // The log is rewritten at each start; wait for this start's bridge line before talking to it.
    await waitUntil(
      () => fs.existsSync(modLog) && fs.statSync(modLog).mtimeMs > started && /\[Auto\] Automation bridge/.test(fs.readFileSync(modLog, "utf8")),
      timeoutMs,
      "the mod's automation bridge to start (Debug build deployed?)",
    );
    await this.waitReady(timeoutMs);
  },

  /** Starts Cities2.exe directly (no Steam relaunch, no launcher) with extra arguments; doesn't wait. */
  launch(extraArgs: string[] = []) {
    const appId = path.join(gameDir, "steam_appid.txt");
    if (!fs.existsSync(appId)) fs.writeFileSync(appId, "949230");
    spawn(gameExe, ["-developerMode", "-uiDeveloperMode", ...extraArgs], { cwd: gameDir, detached: true, stdio: "ignore" }).unref();
  },

  /** Ends the game process; only for runs without the bridge (nothing is saved). */
  async kill() {
    if (!this.isRunning()) return;
    execFileSync("taskkill", ["/IM", "Cities2.exe", "/F"]);
    await waitUntil(() => !this.isRunning(), 30_000, "the game to be killed");
  },

  /** Waits until the game has reached the main menu or a city (it answers the bridge earlier, while starting). */
  async waitReady(timeoutMs = 400_000) {
    const deadline = Date.now() + timeoutMs;
    for (;;) {
      try {
        if ((await bridge.raw<{ ready: boolean }>("GET", "state", undefined, 60_000)).result?.ready) return;
      } catch {
        // not listening yet
      }
      if (Date.now() > deadline) throw new Error("The game did not reach the main menu.");
      await sleep(2000);
    }
  },

  /** Quits through the bridge and waits for the process to end; kills it only if it hangs. */
  async quit(timeoutMs = 120_000) {
    if (!this.isRunning()) return;
    try {
      await bridge.quit();
    } catch {
      // the game may close before it answers
    }
    try {
      await waitUntil(() => !this.isRunning(), timeoutMs, "the game to quit");
    } catch {
      execFileSync("taskkill", ["/IM", "Cities2.exe", "/F"]);
      await waitUntil(() => !this.isRunning(), 30_000, "the game to be killed");
    }
  },

  async restart() {
    await this.quit();
    await this.start();
  },

  /** Starts the game unless it runs; then checks the bridge answers. */
  async ensureRunning() {
    if (!this.isRunning()) await this.start();
    else await this.waitReady();
  },
};

const isProcessRunning = (image: string) => {
  try {
    return execFileSync("tasklist", ["/NH", "/FI", `IMAGENAME eq ${image}`], { encoding: "utf8" }).toLowerCase().includes(image);
  } catch {
    return false;
  }
};

/**
 * A direct start needs a running Steam: without it the game comes up broken (no mods, endless OnGUI errors) and
 * never answers. Starts Steam and gives it time to sign in.
 */
async function ensureSteam() {
  if (isProcessRunning("steam.exe")) return;
  spawn("cmd", ["/c", "start", "", "steam://open/main"], { detached: true, stdio: "ignore" }).unref();
  await waitUntil(() => isProcessRunning("steam.exe"), 120_000, "Steam to start");
  await sleep(30_000);
}

/** Copies Unsquare's manifest and caches aside; {@link restore} puts them back. Geometry assets are left alone. */
export class ModDataBackup {
  private constructor(readonly dir: string) {}

  static create(label = "run"): ModDataBackup {
    const stamp = new Date().toISOString().replace(/[:.]/g, "-");
    const dir = path.join(backupsDir, `${stamp}-${label}`);
    fs.mkdirSync(dir, { recursive: true });
    for (const name of fs.readdirSync(modData))
      if (/\.json(\.bak)?$/.test(name)) fs.copyFileSync(path.join(modData, name), path.join(dir, name));
    return new ModDataBackup(dir);
  }

  /** Puts the saved files back (the game must be closed: it rewrites them). */
  restore() {
    for (const name of fs.readdirSync(this.dir))
      if (fs.statSync(path.join(this.dir, name)).isFile())
        fs.copyFileSync(path.join(this.dir, name), path.join(modData, name.replace(/\.moved$/, "")));
  }

  /** Names of the cached geometry assets (files `UQ_<hash>.Geometry` and their `.cid`). */
  static assetFiles(): string[] {
    return fs.readdirSync(modData).filter((name) => /^UQ_[0-9a-f]+\.Geometry(\.cid)?$/.test(name));
  }

  /** Copies every cached geometry asset into the backup, for tests that let the cache evict. */
  copyAssets() {
    const dir = path.join(this.dir, "assets");
    fs.mkdirSync(dir, { recursive: true });
    for (const name of ModDataBackup.assetFiles()) fs.copyFileSync(path.join(modData, name), path.join(dir, name));
  }

  /** Puts back copied assets that are missing now (evicted); the game must be closed. */
  restoreAssets() {
    const dir = path.join(this.dir, "assets");
    if (!fs.existsSync(dir)) return;
    for (const name of fs.readdirSync(dir))
      if (!fs.existsSync(path.join(modData, name))) fs.copyFileSync(path.join(dir, name), path.join(modData, name));
  }

  /** Moves files out of the mod's data folder into the backup (never deletes); restore brings them back. */
  moveAside(...names: string[]) {
    for (const name of names) {
      const from = path.join(modData, name);
      if (fs.existsSync(from)) fs.renameSync(from, path.join(this.dir, `${name}.moved`));
    }
  }
}

export async function waitUntil(condition: () => boolean, timeoutMs: number, what: string) {
  const deadline = Date.now() + timeoutMs;
  while (!condition()) {
    if (Date.now() > deadline) throw new Error(`Timed out after ${timeoutMs} ms waiting for ${what}.`);
    await sleep(1000);
  }
}
