import { bindValue, trigger, useValue } from "cs2/api";
import { useLocalization } from "cs2/l10n";
import { FloatingButton, Tooltip } from "cs2/ui";
import { ComponentType } from "react";
import icon from "images/unsquare-mode.svg";
import { Guard } from "mods/guard";
import { focusDisabled, ToolButton } from "mods/vanilla";

// Group and names must match UnsquareUISystem on the C# side.
const group = "Unsquare";
const modeActive$ = bindValue<boolean>(group, "modeActive", false);
// Values of the C# ButtonPosition enum; the button is mounted in every place and shows only in the chosen one.
type ButtonPosition = "Bottom" | "TopLeft" | "TopRight";
const buttonPosition$ = bindValue<ButtonPosition>(group, "buttonPosition", "Bottom");

const tooltipKey = "Unsquare.ModeButton.Tooltip";
const tooltipFallback = "Unsquare: deform buildings";
const toggleMode = () => trigger(group, "toggleMode");

// Bottom toolbar, next to the photo mode toggle.
const UnsquareToggle = () => {
    const active = useValue(modeActive$);
    const position = useValue(buttonPosition$);
    const { translate } = useLocalization();
    if (!ToolButton || position !== "Bottom")
        return null;
    return (
        <ToolButton
            src={icon}
            tooltip={translate(tooltipKey, tooltipFallback)}
            selected={active}
            multiSelect={false}
            disabled={false}
            focusKey={focusDisabled}
            onSelect={toggleMode}
        />
    );
};

// Top row, among the other mods' floating buttons.
const UnsquareFloatingToggle = ({ at }: { at: ButtonPosition }) => {
    const active = useValue(modeActive$);
    const position = useValue(buttonPosition$);
    const { translate } = useLocalization();
    if (position !== at)
        return null;
    return (
        <Tooltip tooltip={translate(tooltipKey, tooltipFallback)} direction="down">
            <FloatingButton src={icon} selected={active} onSelect={toggleMode} />
        </Tooltip>
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

export const GuardedTopLeftToggle = () => (
    <Guard name="top left button">
        <UnsquareFloatingToggle at="TopLeft" />
    </Guard>
);

export const GuardedTopRightToggle = () => (
    <Guard name="top right button">
        <UnsquareFloatingToggle at="TopRight" />
    </Guard>
);
