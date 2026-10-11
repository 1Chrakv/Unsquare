import { bindValue, trigger, useValue } from "cs2/api";
import { useLocalization } from "cs2/l10n";
import { FloatingButton, Tooltip } from "cs2/ui";
import icon from "images/unsquare-mode.svg";

// Group and names must match UnsquareUISystem on the C# side.
const group = "Unsquare";
const modeActive$ = bindValue<boolean>(group, "modeActive", false);

export const UnsquareButton = () => {
    const active = useValue(modeActive$);
    const { translate } = useLocalization();
    const tooltip = translate("Unsquare.ModeButton.Tooltip", "Unsquare: deform buildings");

    return (
        <Tooltip tooltip={tooltip}>
            <FloatingButton src={icon} selected={active} onSelect={() => trigger(group, "toggleMode")} />
        </Tooltip>
    );
};
