// Game control from the command line: enable (bridge enable file), start, quit.
import { game } from "../src/game.js";

const command = process.argv[2];
switch (command) {
  case "enable":
    game.enableBridge();
    console.log("Bridge enabled for the next game start (Debug builds only).");
    break;
  case "start":
    await game.start();
    console.log("Game running; bridge answers.");
    break;
  case "quit":
    await game.quit();
    console.log("Game closed.");
    break;
  default:
    console.error("Usage: tsx scripts/game.ts enable|start|quit");
    process.exit(2);
}
