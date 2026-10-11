import { ModRegistrar } from "cs2/modding";
import { UnsquareButton } from "mods/unsquare-button";

const register: ModRegistrar = (moduleRegistry) => {
    moduleRegistry.append("GameBottomRight", UnsquareButton);
};

export default register;
