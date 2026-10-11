import { bindValue, trigger, useValue } from "cs2/api";
import { useLocalization } from "cs2/l10n";
import { ComponentType } from "react";
import icon from "images/unsquare-mode.svg";
import { Guard } from "mods/guard";
import { focusDisabled, ToolButton } from "mods/vanilla";

// Group and names must match UnsquareUISystem on the C# side.
const group = "Unsquare";
const modeActive$ = bindValue<boolean>(group, "modeActive", false);

const UnsquareToggle = () => {
    const active = useValue(modeActive$);
    const { translate } = useLocalization();
    if (!ToolButton)
        return null;
    return (
        <ToolButton
            src={icon}
            tooltip={translate("Unsquare.ModeButton.Tooltip", "Unsquare: deform buildings")}
            selected={active}
            multiSelect={false}
            disabled={false}
            focusKey={focusDisabled}
            onSelect={() => trigger(group, "toggleMode")}
        />
    );
};

export const withUnsquareToggle = (Original: ComponentType<any>) => (props: any) => (
    <>
        <Guard name="toolbar button">
            <UnsquareToggle />
        </Guard>
        <Original {...props} />
    </>
);
