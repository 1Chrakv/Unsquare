import fs from "node:fs";
import { modLog } from "./paths.js";
import { sleep } from "./bridge.js";

/**
 * Reads `Unsquare.Mod.log` from a mark onwards, so a test only sees the lines its own steps wrote. The game
 * rewrites the log at each start; a shrunk file means a restart and is read from the beginning.
 */
export class LogWatch {
  private offset: number;

  private constructor(private file: string) {
    this.offset = fs.existsSync(file) ? fs.statSync(file).size : 0;
  }

  /** Starts watching at the current end of the log. */
  static fromNow(file = modLog) {
    return new LogWatch(file);
  }

  /** Every line written since the mark. */
  lines(): string[] {
    if (!fs.existsSync(this.file)) return [];
    const buffer = fs.readFileSync(this.file);
    const from = buffer.length < this.offset ? 0 : this.offset;
    return buffer
      .subarray(from)
      .toString("utf8")
      .split(/\r?\n/)
      .filter((line) => line.length > 0);
  }

  matching(pattern: RegExp): string[] {
    return this.lines().filter((line) => pattern.test(line));
  }

  /** Waits up to `timeoutMs` for a line matching `pattern` and returns it; throws with the tail of the log otherwise. */
  async waitFor(pattern: RegExp, timeoutMs = 10_000): Promise<string> {
    const deadline = Date.now() + timeoutMs;
    for (;;) {
      const line = this.lines().find((l) => pattern.test(l));
      if (line) return line;
      if (Date.now() > deadline)
        throw new Error(`No log line matching ${pattern} within ${timeoutMs} ms. Last lines:\n${this.lines().slice(-15).join("\n")}`);
      await sleep(200);
    }
  }

  /** The log text since the mark, for failure reports. */
  text = () => this.lines().join("\n");
}
