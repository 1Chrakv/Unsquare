import fs from "node:fs";
import { tokenFile } from "./paths.js";

/** A cage as the bridge reports it (building space, metres). */
export interface CageJson {
  status: "Undeformed" | "Applied" | "Pending" | "Applying" | "Failed";
  settled: boolean;
  target: "Both" | "Building" | "Lot";
  box: { min: Vec3; max: Vec3 };
  lotBox: { min: Vec3; max: Vec3 };
  corners: Vec3[];
  targetOffsets: string;
  appliedOffsets: string;
  targetSpec: string;
  appliedSpec: string;
  deformed: boolean;
  lotDeformed: boolean;
  split: boolean;
  blendMode: string;
  terrainLift: boolean;
  offGround: boolean;
  exceedsLot: boolean;
  hasGhost: boolean;
}

export type Vec3 = [number, number, number];

export interface SessionJson {
  mode: boolean;
  enabled: boolean;
  target: { id: string; ref: string } | null;
  preview: string | null;
  editMode: "Both" | "Building" | "Lot";
  pickKind: HandleKind;
  handle: string | null;
  locked: boolean;
  step: number;
  gizmo: "Move" | "Rotate";
  alignMode: "Angle" | "Flush" | "Full";
  groundFitMode: "BackOnly" | "AllCorners";
  dragging: boolean;
  lastResult: string | null;
  problem: string | null;
  canUndo: boolean;
  canRedo: boolean;
  decisionOpen: boolean;
  cage?: CageJson;
}

export type HandleKind = "Corner" | "Edge" | "Face" | "Cage";
export type Side = "Left" | "Back" | "Right" | "Front";

export interface BuildingJson {
  id: string;
  ref: string;
  prefab: string;
  basePrefab: string;
  variant: boolean;
  position: Vec3;
  yawDegrees: number;
  lotSize: [number, number] | null;
  upgrades: boolean;
  deformable: boolean;
  cage?: CageJson;
}

export interface MeshJson {
  name: string;
  asset: string | null;
  min: Vec3;
  max: Vec3;
  vertices: number;
}

export interface InfoJson {
  prefab: string;
  /** The name the game shows for the building (label). */
  name: string;
  /** Upgrades the building's menu offers (prefab names). */
  upgradeOptions: string[];
  level: number | null;
  renters: number;
  condemned: boolean;
  abandoned: boolean;
  underConstruction: boolean;
  savedCage: boolean;
  upgrades: { id: string; prefab: string }[];
}

/** A prop or attached sub-building; `local`, `forward` and `up` are in the owner's building space. */
export interface SubObjectJson {
  id: string;
  prefab: string;
  world: Vec3;
  local: Vec3;
  forward: Vec3;
  up: Vec3;
}

export interface AreaJson {
  id: string;
  prefab: string;
  local: Vec3[];
}

export interface NetsJson {
  nodes: { id: string; local: Vec3; edges: number; deleted: boolean }[];
  edges: { id: string; prefab: string; start: string; end: string; length: number | null }[];
}

export interface Reply<T = unknown> {
  status: number;
  ok: boolean;
  error?: string;
  result: T;
  frame: number;
  session?: SessionJson;
}

/** The mod refused or failed a command; carries the reply for assertions on 422 results. */
export class BridgeError extends Error {
  constructor(readonly reply: Reply) {
    super(`${reply.status}: ${reply.error ?? "unknown error"}${reply.result ? " " + JSON.stringify(reply.result) : ""}`);
  }
}

/**
 * Typed client of the mod's automation bridge (Debug builds, `Unsquare/Automation`). Every call runs on the game's
 * main thread; long calls are followed through their job URL until they finish.
 */
export class Bridge {
  constructor(
    readonly baseUrl = process.env.UNSQUARE_BRIDGE ?? "http://127.0.0.1:7337",
    private tokenPath = tokenFile,
  ) {}

  /** Sends a request; resolves with the reply even for 4xx/5xx (see {@link call} for the throwing variant). */
  async raw<T = unknown>(method: string, path: string, body?: object, timeoutMs = 600_000): Promise<Reply<T>> {
    const token = fs.readFileSync(this.tokenPath, "utf8").trim();
    const headers = { "X-Unsquare-Token": token, "Content-Type": "application/json" };
    const payload = body ? JSON.stringify({ timeoutMs: Math.min(timeoutMs, 60_000), ...body }) : undefined;
    let response = await fetch(`${this.baseUrl}/v1/${path}`, { method, headers, body: payload });
    const deadline = Date.now() + timeoutMs;
    while (response.status === 202) {
      const pending = (await response.json()) as { job: string };
      if (Date.now() > deadline) throw new Error(`${method} ${path} still running after ${timeoutMs} ms`);
      await sleep(250);
      response = await fetch(`${this.baseUrl}${pending.job}`, { headers });
    }
    const json = (await response.json()) as Omit<Reply<T>, "status">;
    return { status: response.status, ...json };
  }

  /** Like {@link raw}, but throws {@link BridgeError} unless the command succeeded. */
  async call<T = unknown>(method: string, path: string, body?: object, timeoutMs?: number): Promise<Reply<T>> {
    const reply = await this.raw<T>(method, path, body, timeoutMs);
    if (!reply.ok) throw new BridgeError(reply);
    return reply;
  }

  // --- game -------------------------------------------------------------------------------------------------------
  ping = () => this.call<{ version: string; configuration: string; builtUtc: string }>("GET", "ping");
  state = () =>
    this.call<{ gameMode: string; loading: boolean; ready: boolean; inGame: boolean; save: string | null; frame: number; speed: number }>(
      "GET",
      "state",
    );
  load = (save: string) => this.call<{ save: string }>("POST", "game/load", { save }, 900_000);
  save = (name: string) => this.call("POST", "game/save", { name }, 300_000);
  mainMenu = () => this.call("POST", "game/mainMenu", {}, 300_000);
  quit = () => this.raw("POST", "game/quit", {});
  speed = (speed: number) => this.call("POST", "game/speed", { speed });
  mark = (text: string) => this.call("POST", "log/mark", { text });
  /** Runs the simulation for `days` (or `frames`) at full speed, then pauses it. */
  simulate = (span: { days?: number; frames?: number; speed?: number }) =>
    this.call<{ frames: number }>("POST", "game/simulate", span, 3_600_000);
  locale = (locale: string) => this.call<{ previous: string; locale: string }>("POST", "game/locale", { locale });
  /** Zones the cells around points (building space with `building`, else world xz); zone "None" unzones. */
  paintZone = (body: { zone: string; points: [number, number][]; building?: string; radius?: number; onlyEmpty?: boolean }) =>
    this.call<{ cells: number; blocks: number }>("POST", "zone/paint", body);
  /** Starts a level-up of a zoned building through the game's own path; the simulation must run meanwhile. */
  levelUp = (ref: string, maxFrames?: number) =>
    this.call<{ underConstruction: boolean }>("POST", `buildings/${encodeURIComponent(ref)}/levelUp`, { maxFrames }, 3_600_000);

  // --- buildings --------------------------------------------------------------------------------------------------
  async find(query: {
    prefab?: string;
    near?: [number, number];
    radius?: number;
    variant?: boolean;
    upgrades?: boolean;
    limit?: number;
  }): Promise<BuildingJson[]> {
    const params = new URLSearchParams();
    for (const [key, value] of Object.entries(query))
      if (value !== undefined) params.set(key, Array.isArray(value) ? value.join(",") : String(value));
    const reply = await this.call<{ buildings: BuildingJson[] }>("GET", `buildings?${params}`);
    return reply.result.buildings;
  }

  building = async (ref: string) =>
    (await this.call<BuildingJson>("GET", `buildings/${encodeURIComponent(ref)}`)).result;

  // --- session ----------------------------------------------------------------------------------------------------
  session = async () => (await this.call<SessionJson>("GET", "session")).result;
  mode = (on: boolean) => this.call("POST", "session/mode", { on });
  select = (building: string) => this.call<{ id: string; ref: string }>("POST", "session/select", { building });
  clearTarget = () => this.call("POST", "session/clearTarget", {});
  stepBack = () => this.call<{ result: string }>("POST", "session/stepBack", {});
  pick = (kind: HandleKind) => this.call("POST", "session/pick", { kind });
  handle = (kind: HandleKind, index = 0) => this.call("POST", "session/handle", { kind, index });
  clearHandle = () => this.call("POST", "session/handle", { clear: true });
  nextHandle = () => this.call("POST", "session/nextHandle", {});
  previousHandle = () => this.call("POST", "session/previousHandle", {});
  /** Moves the selected handle by `direction` × the current step (building space: x width, y up, z street side). */
  nudge = (direction: Vec3) => this.call<{ result: string }>("POST", "session/nudge", { direction });
  cycleStep = () => this.call<{ result: string }>("POST", "session/step", {});
  /** A drag over `frames` frames; `hold` keeps it active until {@link dragEnd}, `cancelAt` right-clicks at that frame. */
  drag = (body: {
    translate?: Vec3;
    rotate?: { axis: number; degrees: number };
    frames?: number;
    cancelAt?: number;
    hold?: boolean;
    part?: number;
  }) => this.call<{ result?: string; cancelled?: boolean; held?: boolean; frames: number }>("POST", "session/drag", body);
  dragEnd = (cancel = false) => this.call("POST", "session/dragEnd", { cancel });
  undo = () => this.call("POST", "session/undo", {});
  redo = () => this.call("POST", "session/redo", {});
  reset = () => this.call<{ result: string }>("POST", "session/reset", {});
  resetHandle = () => this.call<{ result: string }>("POST", "session/resetHandle", {});
  resetAll = () => this.call<{ result: string }>("POST", "session/resetAll", {});
  clearHistory = () => this.call("POST", "session/clearHistory", {});
  toggleTerrain = () => this.call<{ result: string }>("POST", "session/terrain", {});
  fit = () => this.call<{ result: string }>("POST", "session/fit", {});
  fitMode = (mode: "BackOnly" | "AllCorners") => this.call("POST", "session/fitMode", { mode });
  align = (side: Side) => this.call<{ result: string }>("POST", "session/align", { side });
  level = (side: Side) => this.call<{ result: string }>("POST", "session/level", { side });
  alignMode = (mode: "Angle" | "Flush" | "Full") => this.call("POST", "session/alignMode", { mode });
  editMode = (mode: "Both" | "Building" | "Lot") => this.call("POST", "session/editMode", { mode });
  blend = (mode: "None" | "All") => this.call<{ result: string }>("POST", "session/blend", { mode });
  /** follow "lot": the building follows the lot (Alt+K); "building": the lot follows the building (Alt+Shift+K). */
  link = (follow: "lot" | "building") => this.call<{ result: string }>("POST", "session/link", { follow });
  gizmo = (mode: "Move" | "Rotate") => this.call("POST", "session/gizmo", { mode });
  describe = () => this.call<{ result: string }>("POST", "session/describe", {});

  // --- probes -----------------------------------------------------------------------------------------------------
  info = async (ref: string) => (await this.call<InfoJson>("GET", `buildings/${encodeURIComponent(ref)}/info`)).result;
  subObjects = async (ref: string) =>
    (await this.call<{ subObjects: SubObjectJson[] }>("GET", `buildings/${encodeURIComponent(ref)}/subobjects`)).result.subObjects;
  areas = async (ref: string) =>
    (await this.call<{ areas: AreaJson[] }>("GET", `buildings/${encodeURIComponent(ref)}/areas`)).result.areas;
  nets = async (ref: string) => (await this.call<NetsJson>("GET", `buildings/${encodeURIComponent(ref)}/nets`)).result;
  lot = async (ref: string) =>
    (await this.call<{ front: Vec3; right: Vec3; back: Vec3; left: Vec3 }>("GET", `buildings/${encodeURIComponent(ref)}/lot`)).result;
  /** Terrain heights at building-space ground points, relative to the building's ground (or world with no building). */
  heights = async (points: [number, number][], building?: string) =>
    (await this.call<{ heights: number[] }>("POST", "terrain/heights", { points, building })).result.heights;
  meshes = async (ref: string) =>
    (await this.call<{ prefab: string; meshes: (MeshJson & { lods: MeshJson[] })[] }>("GET", `buildings/${encodeURIComponent(ref)}/meshes`))
      .result;
  overlay = async () =>
    (
      await this.call<{ lineRadius: number; handleDiameter: number; selectedHandleDiameter: number; axisLength: number; gizmoScreenSize: number }>(
        "GET",
        "overlay",
      )
    ).result;
  /** Zone cells under the lot and in the one-cell ring around it, counted by zone prefab name ("None" = unzoned). */
  zone = async (ref: string) =>
    (await this.call<{ inside: Record<string, number>; ring: Record<string, number> }>("GET", `buildings/${encodeURIComponent(ref)}/zone`))
      .result;
  settings = async () => (await this.call<Record<string, unknown>>("GET", "settings")).result;
  setSettings = async (values: Record<string, unknown>) => (await this.call<Record<string, unknown>>("POST", "settings", { values })).result;
  defaultSettings = async () => (await this.call<Record<string, unknown>>("POST", "settings/defaults", {})).result;

  // --- waiting ----------------------------------------------------------------------------------------------------
  frames = (frames: number) => this.call("POST", "wait/frames", { frames });
  /** Waits until the building (default: the target) has baked and shows the requested shape. */
  settled = async (building?: string, maxFrames = 1800) =>
    (await this.call<{ cage: CageJson }>("POST", "wait/settled", { building, maxFrames })).result.cage;
}

export const sleep = (ms: number) => new Promise((resolve) => setTimeout(resolve, ms));

export const bridge = new Bridge();
