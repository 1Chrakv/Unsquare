import { getModule } from "cs2/modding";

// Vanilla modules, looked up once; a game update may rename them, so failures only hide our UI.
export function tryGetModule(path: string, name: string): any {
    try {
        return getModule(path, name);
    } catch (e) {
        console.warn(`[Unsquare] UI module ${path}#${name} not found`, e);
        return undefined;
    }
}

export const ToolButton = tryGetModule("game-ui/game/components/tool-options/tool-button/tool-button.tsx", "ToolButton");
export const focusDisabled = tryGetModule("game-ui/common/focus/focus-key.ts", "FOCUS_DISABLED");
