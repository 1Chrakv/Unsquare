import { ModRegistrar } from "cs2/modding";
import { GuardedToolWindow } from "mods/tool-window";
import { withUnsquareToggle } from "mods/unsquare-toggle";

// The top toolbar toggles (underground mode, photo mode, ...); our button goes right next to them.
const togglesModule = "game-ui/game/components/toolbar/top/toggles.tsx";

const register: ModRegistrar = (moduleRegistry) => {
    moduleRegistry.extend(togglesModule, "PhotoModeToggle", withUnsquareToggle);
    // Floating window above the building edited in Unsquare mode.
    moduleRegistry.append("Game", GuardedToolWindow);
};

export default register;
