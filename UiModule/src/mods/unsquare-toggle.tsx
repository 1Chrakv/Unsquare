import { bindValue, trigger, useValue } from "cs2/api";
import { useLocalization } from "cs2/l10n";
import { getModule } from "cs2/modding";
import { Component, ComponentType, ReactNode } from "react";
import icon from "images/unsquare-mode.svg";

// Group and names must match UnsquareUISystem on the C# side.
const group = "Unsquare";
const modeActive$ = bindValue<boolean>(group, "modeActive", false);

// Vanilla modules, looked up once; a game update may rename them, so failures only hide our button.
const ToolButton = tryGetModule("game-ui/game/components/tool-options/tool-button/tool-button.tsx", "ToolButton");
const focusDisabled = tryGetModule("game-ui/common/focus/focus-key.ts", "FOCUS_DISABLED");

function tryGetModule(path: string, name: string): any {
    try {
        return getModule(path, name);
    } catch (e) {
        console.warn(`[Unsquare] UI module ${path}#${name} not found`, e);
        return undefined;
    }
}

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

// Keeps the vanilla toggles alive if our button throws.
class Guard extends Component<{ children?: ReactNode }, { failed: boolean }> {
    state = { failed: false };

    static getDerivedStateFromError() {
        return { failed: true };
    }

    componentDidCatch(error: unknown) {
        console.error("[Unsquare] toolbar button failed", error);
    }

    render() {
        return this.state.failed ? null : this.props.children ?? null;
    }
}

export const withUnsquareToggle = (Original: ComponentType<any>) => (props: any) => (
    <>
        <Guard>
            <UnsquareToggle />
        </Guard>
        <Original {...props} />
    </>
);
