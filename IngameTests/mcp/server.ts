// MCP server "unsquare-game": lets Claude look at and drive the running game through the mod's automation bridge
// (Debug builds). Started by Claude Code from the repo's .mcp.json over stdio; it never starts the game itself.
import fs from "node:fs";
import path from "node:path";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { z } from "zod";
import { bridge, BridgeError, type GizmoPart, type PointerTarget } from "../src/bridge.js";
import { LogWatch } from "../src/log.js";
import { modLog, runsDir } from "../src/paths.js";
import { lookAt, Presets, type Preset } from "../src/view.js";

const server = new McpServer({ name: "unsquare-game", version: "1.0.0" });

type Content = { type: "text"; text: string } | { type: "image"; data: string; mimeType: string };
const text = (value: unknown): { content: Content[] } => ({
  content: [{ type: "text", text: typeof value === "string" ? value : JSON.stringify(value, null, 2) }],
});

/** Refuses while a test run holds the lock: two drivers on one game reset each other's state. */
function checkNoRun() {
  const lock = path.join(runsDir, "run.lock");
  if (!fs.existsSync(lock)) return;
  const pid = Number(fs.readFileSync(lock, "utf8"));
  try {
    process.kill(pid, 0);
  } catch {
    return;
  }
  throw new Error(`A test run (process ${pid}) is using the game; wait until it ends.`);
}

/** Runs a tool body; bridge errors become readable text instead of protocol errors. */
function tool<Shape extends z.ZodRawShape>(
  name: string,
  description: string,
  shape: Shape,
  run: (args: z.infer<z.ZodObject<Shape>>) => Promise<{ content: Content[] }>,
  options: { readOnly?: boolean } = {},
) {
  server.registerTool(name, { description, inputSchema: shape, annotations: { readOnlyHint: options.readOnly ?? false } }, (async (
    args: z.infer<z.ZodObject<Shape>>,
  ) => {
    try {
      if (!options.readOnly) checkNoRun();
      return await run(args);
    } catch (error) {
      const message = error instanceof BridgeError ? `${error.message}` : String((error as Error).message ?? error);
      return { ...text(`Failed: ${message}`), isError: true };
    }
  }) as never);
}

const pointer = z
  .object({
    x: z.number().optional(),
    y: z.number().optional(),
    handle: z.string().optional().describe("Cage handle of the target, e.g. Corner3, Face5, Cage0"),
    gizmo: z.enum(["arrowX", "arrowY", "arrowZ", "plane", "ringX", "ringY", "ringZ"]).optional(),
    world: z.tuple([z.number(), z.number(), z.number()]).optional(),
  })
  .describe("Pixels (top-left origin), a handle, a gizmo part of the selected handle, or a world point");

function toTarget(p: z.infer<typeof pointer>): PointerTarget {
  if (p.handle) return { handle: p.handle };
  if (p.gizmo) return { gizmo: p.gizmo as GizmoPart };
  if (p.world) return { world: p.world };
  if (p.x !== undefined && p.y !== undefined) return { x: p.x, y: p.y };
  throw new Error("A pointer needs x/y, handle, gizmo or world.");
}

async function shotContent(name: string, crop: "screen" | "target", maxPx: number, ui: boolean): Promise<{ content: Content[] }> {
  const shot = await bridge.screenshot(name, { crop, maxPx, ui });
  const data = fs.readFileSync(shot.path).toString("base64");
  return {
    content: [
      { type: "image", data, mimeType: "image/jpeg" },
      { type: "text", text: `${shot.width}x${shot.height} of a ${shot.screen.width}x${shot.screen.height} screen${shot.crop ? `, crop ${JSON.stringify(shot.crop)}` : ""}` },
    ],
  };
}

tool("game_state", "Game mode, loaded save, frame and the Unsquare edit session (mode, target, handle, cage).", {}, async () => {
  const [state, session] = await Promise.all([bridge.state(), bridge.session()]);
  return text({ state, session });
}, { readOnly: true });

tool(
  "find_buildings",
  "Buildings by prefab name part and/or near a world point [x, z]. Returns refs usable everywhere (Prefab@x,z).",
  { prefab: z.string().optional(), near: z.tuple([z.number(), z.number()]).optional(), radius: z.number().optional(), limit: z.number().optional() },
  async (args) => text(await bridge.find(args)),
  { readOnly: true },
);

const probes = ["info", "subobjects", "areas", "nets", "lot", "meshes", "zone"] as const;
tool(
  "probe",
  "Read-only data about a building: info, subobjects (props), areas, nets (paths), lot, meshes or zone.",
  { ref: z.string(), kind: z.enum(probes) },
  async ({ ref, kind }) => text((await bridge.call("GET", `buildings/${encodeURIComponent(ref)}/${kind}`)).result),
  { readOnly: true },
);

const sessionCommands = [
  "mode", "select", "clearTarget", "stepBack", "pick", "handle", "nextHandle", "previousHandle", "nudge", "cycleStep",
  "drag", "dragEnd", "undo", "redo", "reset", "resetHandle", "resetAll", "terrain", "fit", "fitMode", "align", "level",
  "alignMode", "editMode", "blend", "link", "gizmo", "describe",
] as const;
tool(
  "session",
  "Runs an Unsquare edit-session command (the same the hotkeys and mouse use), e.g. select {building}, handle {kind, index}, nudge {axis, steps}, undo.",
  { command: z.enum(sessionCommands), args: z.record(z.string(), z.unknown()).optional() },
  async ({ command, args }) => text(await bridge.call("POST", `session/${command}`, args ?? {})),
);

tool("wait_settled", "Waits until the building (default: the target) has baked its requested shape.", { building: z.string().optional() }, async ({ building }) =>
  text(await bridge.settled(building)),
);

tool(
  "camera",
  "Orbits the game camera: focus (building ref) or point, yaw (0 = the building's street front), pitch and distance.",
  { focus: z.string().optional(), point: z.tuple([z.number(), z.number(), z.number()]).optional(), yaw: z.number().optional(), pitch: z.number().optional(), distance: z.number().optional() },
  async (args) => text(await bridge.setCamera(args)),
);

tool(
  "screenshot",
  "A JPEG of the game now. crop 'target' frames the edited cage; ui false hides the game UI. Returns the image.",
  { crop: z.enum(["screen", "target"]).optional(), maxPx: z.number().optional(), ui: z.boolean().optional() },
  async ({ crop = "screen", maxPx = 1024, ui = true }) => shotContent(`mcp-${Date.now()}`, crop, maxPx, ui),
  { readOnly: true },
);

tool(
  "look_at",
  `Points the camera at a building from a preset (${Object.keys(Presets).join(", ")}) and returns a screenshot.`,
  { ref: z.string(), preset: z.enum(Object.keys(Presets) as [Preset, ...Preset[]]).optional(), distance: z.number().optional(), crop: z.enum(["screen", "target"]).optional(), maxPx: z.number().optional() },
  async ({ ref, preset = "front", distance, crop = "screen", maxPx = 1024 }) => {
    await lookAt(ref, preset, distance);
    return shotContent(`mcp-${Date.now()}`, crop, maxPx, true);
  },
);

tool(
  "screen_point",
  "Where a handle, gizmo part or world point is on screen (pixels, top-left origin).",
  { handle: z.string().optional(), gizmo: pointer.shape.gizmo, world: pointer.shape.world },
  async (args) => {
    const query = args.handle ? { handle: args.handle } : args.gizmo ? { gizmo: args.gizmo as GizmoPart } : args.world ? { world: args.world } : undefined;
    if (!query) throw new Error("Give handle, gizmo or world.");
    return text(await bridge.screenPoint(query));
  },
  { readOnly: true },
);

tool(
  "input_mouse",
  "Real mouse input: move, press, release, click or drag (at → to over frames), left or right button.",
  { action: z.enum(["move", "press", "release", "click", "drag"]), at: pointer.optional(), to: pointer.optional(), button: z.enum(["left", "right"]).optional(), frames: z.number().optional() },
  async ({ action, at, to, button, frames }) =>
    text(await bridge.mouse(action, { at: at ? toTarget(at) : undefined, to: to ? toTarget(to) : undefined, button, frames })),
);

tool(
  "input_key",
  "Presses a binding by name (e.g. UnsquareUndo, with its current keys) or raw Unity keys together (e.g. [\"LeftCtrl\"]).",
  { binding: z.string().optional(), keys: z.array(z.string()).optional() },
  async ({ binding, keys }) => text(await bridge.key(binding ? { binding } : { keys: keys ?? [] })),
);

tool(
  "log",
  "The tail of the mod's log (Unsquare.Mod.log), optionally only lines after the last line containing a marker.",
  { lines: z.number().optional(), since: z.string().optional() },
  async ({ lines = 60, since }) => {
    let all = LogWatch.fromStart(modLog).lines();
    if (since) {
      const at = all.map((line) => line.includes(since)).lastIndexOf(true);
      if (at >= 0) all = all.slice(at);
    }
    return text(all.slice(-lines).join("\n"));
  },
  { readOnly: true },
);

tool(
  "load",
  "Loads a save by name (the fixture UnsquareTest-Fixture or a test save); takes one to two minutes.",
  { save: z.string() },
  async ({ save }) => text(await bridge.load(save)),
);

tool("save", "Saves the city as UnsquareTest-<name>; the bridge refuses other names and the fixture.", { name: z.string() }, async ({ name }) =>
  text(await bridge.save(name.startsWith("UnsquareTest-") ? name : `UnsquareTest-${name}`)),
);

await server.connect(new StdioServerTransport());
