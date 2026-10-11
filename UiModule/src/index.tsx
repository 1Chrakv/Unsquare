import { ModRegistrar } from "cs2/modding";
import { withUnsquareToggle } from "mods/unsquare-toggle";

// The top toolbar toggles (underground mode, photo mode, ...); our button goes right next to them.
const togglesModule = "game-ui/game/components/toolbar/top/toggles.tsx";

const register: ModRegistrar = (moduleRegistry) => {
    moduleRegistry.extend(togglesModule, "PhotoModeToggle", withUnsquareToggle);
};

export default register;
