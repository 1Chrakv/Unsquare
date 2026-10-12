import { bindValue, trigger, useValue } from "cs2/api";
import { useLocalization } from "cs2/l10n";
import cornerIcon from "images/pick-corner.svg";
import fitGroundIcon from "images/fit-ground.svg";
import edgeIcon from "images/pick-edge.svg";
import faceIcon from "images/pick-face.svg";
import resetBuildingIcon from "images/reset-building.svg";
import resetHandleIcon from "images/reset-handle.svg";
import terrainLiftIcon from "images/terrain-lift.svg";
import { Guard } from "mods/guard";
import { focusDisabled, ToolButton } from "mods/vanilla";
import styles from "./tool-window.module.scss";

// Group, names and fields must match UnsquareUISystem and ToolWindowAnchor on the C# side.
const group = "Unsquare";

interface ToolWindowAnchor {
    visible: boolean;
    x: number; // 0..1 from the left
    y: number; // 0..1 from the top
}

const toolWindow$ = bindValue<ToolWindowAnchor>(group, "toolWindow", { visible: false, x: 0, y: 0 });
const pickKind$ = bindValue<string>(group, "pickKind", "Edge");
const hasHandle$ = bindValue<boolean>(group, "hasHandle", false);
const terrainLift$ = bindValue<boolean>(group, "terrainLift", false);
const canLiftTerrain$ = bindValue<boolean>(group, "canLiftTerrain", false);

// Values are the C# CageHandleKind names.
const pickKinds = [
    { kind: "Corner", icon: cornerIcon, fallback: "Pick corners" },
    { kind: "Edge", icon: edgeIcon, fallback: "Pick edges" },
    { kind: "Face", icon: faceIcon, fallback: "Pick faces" },
];

const ToolWindow = () => {
    const anchor = useValue(toolWindow$);
    const pickKind = useValue(pickKind$);
    const hasHandle = useValue(hasHandle$);
    const terrainLift = useValue(terrainLift$);
    const canLiftTerrain = useValue(canLiftTerrain$);
    const { translate } = useLocalization();
    if (!ToolButton || !anchor.visible)
        return null;
    return (
        <div className={styles.anchor} style={{ left: `${anchor.x * 100}vw`, top: `${anchor.y * 100}vh` }}>
            <div className={styles.window}>
                {pickKinds.map(({ kind, icon, fallback }) => (
                    <ToolButton
                        key={kind}
                        src={icon}
                        tooltip={translate(`Unsquare.ToolWindow.${kind}`, fallback)}
                        selected={pickKind === kind}
                        multiSelect={false}
                        disabled={false}
                        focusKey={focusDisabled}
                        onSelect={() => trigger(group, "selectPickKind", kind)}
                    />
                ))}
                <div className={styles.divider} />
                <ToolButton
                    src={resetHandleIcon}
                    tooltip={translate("Unsquare.ToolWindow.ResetHandle", "Reset the selected corner, edge or face")}
                    selected={false}
                    multiSelect={false}
                    disabled={!hasHandle}
                    focusKey={focusDisabled}
                    onSelect={() => trigger(group, "resetHandle")}
                />
                <ToolButton
                    src={resetBuildingIcon}
                    tooltip={translate("Unsquare.ToolWindow.ResetBuilding", "Reset the whole building")}
                    selected={false}
                    multiSelect={false}
                    disabled={false}
                    focusKey={focusDisabled}
                    onSelect={() => trigger(group, "resetBuilding")}
                />
                <div className={styles.divider} />
                <ToolButton
                    src={terrainLiftIcon}
                    tooltip={translate("Unsquare.ToolWindow.TerrainLift", "Lift the terrain with the building")}
                    selected={terrainLift}
                    multiSelect={false}
                    disabled={!canLiftTerrain}
                    focusKey={focusDisabled}
                    onSelect={() => trigger(group, "toggleTerrainLift")}
                />
                <ToolButton
                    src={fitGroundIcon}
                    tooltip={translate("Unsquare.ToolWindow.FitToGround", "Fit the back corners to the original ground")}
                    selected={false}
                    multiSelect={false}
                    disabled={false}
                    focusKey={focusDisabled}
                    onSelect={() => trigger(group, "fitToGround")}
                />
            </div>
        </div>
    );
};

export const GuardedToolWindow = () => (
    <Guard name="tool window">
        <ToolWindow />
    </Guard>
);
