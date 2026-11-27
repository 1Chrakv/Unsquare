import { bridge } from "./bridge.js";

/**
 * The game's UI as DOM, through the cohtml inspector (the game runs with -uiDeveloperMode): DevTools protocol
 * over a websocket. Class names are hashed per build, so Unsquare's buttons are found by their icon file.
 */
const inspector = process.env.UNSQUARE_UI_INSPECTOR ?? "ws://127.0.0.1:9444/devtools/page/0";

/** Evaluates a JavaScript expression in the game UI and returns its (JSON) value. */
export function evaluate<T>(expression: string, timeoutMs = 5000): Promise<T> {
  return new Promise<T>((resolve, reject) => {
    const ws = new WebSocket(inspector);
    const timer = setTimeout(() => {
      ws.close();
      reject(new Error(`The UI inspector at ${inspector} did not answer within ${timeoutMs} ms.`));
    }, timeoutMs);
    ws.onerror = () => {
      clearTimeout(timer);
      reject(new Error(`The UI inspector at ${inspector} is not reachable (game started with -uiDeveloperMode?).`));
    };
    ws.onopen = () => ws.send(JSON.stringify({ id: 1, method: "Runtime.evaluate", params: { expression, returnByValue: true } }));
    ws.onmessage = (message) => {
      const reply = JSON.parse(String(message.data)) as { id?: number; result?: { result?: { value?: unknown }; exceptionDetails?: unknown } };
      if (reply.id !== 1) return;
      clearTimeout(timer);
      ws.close();
      if (reply.result?.exceptionDetails) reject(new Error(`UI evaluation failed: ${JSON.stringify(reply.result.exceptionDetails)}`));
      else resolve(reply.result?.result?.value as T);
    };
  });
}

export interface Rect {
  x: number;
  y: number;
  width: number;
  height: number;
}

export interface UiButton extends Rect {
  /** Laid out and on screen. */
  visible: boolean;
  disabled: boolean;
  /** The mode button's highlight, or the active option of a segmented toggle. */
  active: boolean;
}

/** Unsquare's icons, e.g. "pick-face", "undo", "unsquare-mode" (file names under UiModule/src/images). */
export type Icon = string;

const rectOf = (element: string) =>
  `(() => { const r = ${element}.getBoundingClientRect(); return { x: r.left, y: r.top, width: r.width, height: r.height }; })()`;

/** The button showing `icon`, or null when it isn't in the UI. */
export async function button(icon: Icon): Promise<UiButton | null> {
  return evaluate<UiButton | null>(`(() => {
    const img = document.querySelector('img[src$="/${icon}.svg"]');
    const b = img && (img.closest('button') || img.parentElement);
    if (!b) return null;
    const r = b.getBoundingClientRect();
    const parent = String(b.parentElement && b.parentElement.className);
    return {
      x: r.left, y: r.top, width: r.width, height: r.height,
      visible: r.width > 0 && r.height > 0 && r.right > 0 && r.bottom > 0 && r.left < innerWidth && r.top < innerHeight,
      disabled: b.hasAttribute('disabled'),
      active: b.classList.contains('selected') || /(^| )segmentActive_/.test(parent),
    };
  })()`);
}

/** The tool window's box (the frame around the sections), or null while it isn't shown. */
export async function toolWindow(): Promise<Rect | null> {
  return evaluate<Rect | null>(`(() => {
    const img = document.querySelector('img[src$="/pick-corner.svg"]');
    const w = img && img.closest('[class^="window_"]');
    if (!w) return null;
    const r = ${rectOf("w")};
    return r.width > 0 ? r : null;
  })()`);
}

/** The text of every element matching a CSS selector (e.g. the hint line). */
export const texts = (selector: string) =>
  evaluate<string[]>(`[...document.querySelectorAll(${JSON.stringify(selector)})].map(e => e.textContent.trim())`);

/** Clicks the button showing `icon` with the bridge's mouse, at its centre; throws unless it is visible. */
export async function clickButton(icon: Icon) {
  const found = await button(icon);
  if (!found?.visible) throw new Error(`The ${icon} button is not on screen.`);
  return bridge.mouse("click", { at: { x: found.x + found.width / 2, y: found.y + found.height / 2 }, settleFrames: 4 });
}

export const centre = (r: Rect) => ({ x: r.x + r.width / 2, y: r.y + r.height / 2 });
