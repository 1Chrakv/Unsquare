import { bindValue, trigger, useValue } from "cs2/api";
import { useLocalization } from "cs2/l10n";
import { ReactNode } from "react";
import alignAngleIcon from "images/align-angle.svg";
import alignBackIcon from "images/align-back.svg";
import alignFlushIcon from "images/align-flush.svg";
import alignFullIcon from "images/align-full.svg";
import alignGroundBackIcon from "images/align-ground-back.svg";
import alignGroundLeftIcon from "images/align-ground-left.svg";
import alignGroundRightIcon from "images/align-ground-right.svg";
import alignLeftIcon from "images/align-left.svg";
import alignRightIcon from "images/align-right.svg";
import cornerIcon from "images/pick-corner.svg";
import fitGroundIcon from "images/fit-ground.svg";
import edgeIcon from "images/pick-edge.svg";
import faceIcon from "images/pick-face.svg";
import groundFitAllIcon from "images/ground-fit-all.svg";
import groundFitBackIcon from "images/ground-fit-back.svg";
import resetBuildingIcon from "images/reset-building.svg";
import resetHandleIcon from "images/reset-handle.svg";
import terrainLiftIcon from "images/terrain-lift.svg";
import { Guard } from "mods/guard";
import { SegmentedToggle, SegmentOption } from "mods/segmented-toggle";
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
const alignMode$ = bindValue<string>(group, "alignMode", "Angle");
const groundFitMode$ = bindValue<string>(group, "groundFitMode", "BackOnly");

// Values are the C# CageHandleKind names.
const pickKinds: ToggleEntry[] = [
    { value: "Corner", icon: cornerIcon, fallback: "Pick corners" },
    { value: "Edge", icon: edgeIcon, fallback: "Pick edges" },
    { value: "Face", icon: faceIcon, fallback: "Pick faces" },
];

// Values are the C# AlignSide and AlignMode names; sides as seen from the street.
const alignSides = [
    { side: "Left", icon: alignLeftIcon, fallback: "Align the left side to its neighbour (Alt+Shift+Left)" },
    { side: "Back", icon: alignBackIcon, fallback: "Align the back to its neighbour (Alt+Shift+Up)" },
    { side: "Right", icon: alignRightIcon, fallback: "Align the right side to its neighbour (Alt+Shift+Right)" },
];

const groundSides = [
    { side: "Left", icon: alignGroundLeftIcon, fallback: "Level the left side with its neighbour (Ctrl+Alt+Left)" },
    { side: "Back", icon: alignGroundBackIcon, fallback: "Level the back with its neighbour (Ctrl+Alt+Up)" },
    { side: "Right", icon: alignGroundRightIcon, fallback: "Level the right side with its neighbour (Ctrl+Alt+Right)" },
];

const alignModes: ToggleEntry[] = [
    { value: "Angle", icon: alignAngleIcon, fallback: "Align mode: angle only (Alt+A)" },
    { value: "Flush", icon: alignFlushIcon, fallback: "Align mode: flush (Alt+A)" },
    { value: "Full", icon: alignFullIcon, fallback: "Align mode: full (Alt+A)" },
];

// Values are the C# GroundFitMode names.
const groundFitModes: ToggleEntry[] = [
    { value: "BackOnly", icon: groundFitBackIcon, fallback: "Fit mode: back only, street side at road level (Alt+F)" },
    { value: "AllCorners", icon: groundFitAllIcon, fallback: "Fit mode: all corners follow the ground (Alt+F)" },
];

interface ToggleEntry {
    value: string;
    icon: string;
    fallback: string;
}

// Toggle options with localised tooltips; the key is the prefix followed by the value.
const toOptions = (entries: ToggleEntry[], keyPrefix: string,
    translate: ReturnType<typeof useLocalization>["translate"]): SegmentOption[] =>
    entries.map(({ value, icon, fallback }) =>
        ({ value, icon, tooltip: translate(`${keyPrefix}${value}`, fallback) ?? fallback }));

interface SectionProps {
    title: string | null;
    top: ReactNode;
    bottom?: ReactNode;
    // Rows start at the left edge, so controls in the bottom row sit under their top-row counterparts.
    columns?: boolean;
}

// A labelled group of controls in two rows: toggles always go in the top row.
const Section = ({ title, top, bottom, columns }: SectionProps) => (
    <div className={styles.section}>
        <div className={styles.sectionHeader}>{title}</div>
        <div className={columns ? styles.rowColumns : styles.row}>{top}</div>
        <div className={columns ? styles.rowColumns : styles.row}>{bottom}</div>
    </div>
);

// A vanilla tool button with the defaults every button in this window shares.
const ActionButton = ({ icon, tooltip, disabled, selected, onSelect }: {
    icon: string;
    tooltip: string | null;
    disabled?: boolean;
    selected?: boolean;
    onSelect: () => void;
}) => (
    <ToolButton
        src={icon}
        tooltip={tooltip}
        selected={selected ?? false}
        multiSelect={false}
        disabled={disabled ?? false}
        focusKey={focusDisabled}
        onSelect={onSelect}
    />
);

const ToolWindow = () => {
    const anchor = useValue(toolWindow$);
    const pickKind = useValue(pickKind$);
    const hasHandle = useValue(hasHandle$);
    const terrainLift = useValue(terrainLift$);
    const canLiftTerrain = useValue(canLiftTerrain$);
    const alignMode = useValue(alignMode$);
    const groundFitMode = useValue(groundFitMode$);
    const { translate } = useLocalization();
    if (!ToolButton || !anchor.visible)
        return null;
    return (
        <div className={styles.anchor} style={{ left: `${anchor.x * 100}vw`, top: `${anchor.y * 100}vh` }}>
            <div className={styles.window}>
                <Section
                    title={translate("Unsquare.ToolWindow.Section.Pick", "Pick")}
                    top={<SegmentedToggle
                        options={toOptions(pickKinds, "Unsquare.ToolWindow.", translate)}
                        value={pickKind}
                        onSelect={kind => trigger(group, "selectPickKind", kind)}
                    />}
                />
                <div className={styles.divider} />
                <Section
                    title={translate("Unsquare.ToolWindow.Section.Terrain", "Terrain")}
                    top={<>
                        <ActionButton
                            icon={terrainLiftIcon}
                            tooltip={translate("Unsquare.ToolWindow.TerrainLift", "Lift the terrain with the building")}
                            selected={terrainLift}
                            disabled={!canLiftTerrain}
                            onSelect={() => trigger(group, "toggleTerrainLift")}
                        />
                        <div className={styles.spacer} />
                        <SegmentedToggle
                            options={toOptions(groundFitModes, "Unsquare.ToolWindow.GroundFitMode", translate)}
                            value={groundFitMode}
                            onSelect={mode => trigger(group, "selectGroundFitMode", mode)}
                        />
                    </>}
                    bottom={<ActionButton
                        icon={fitGroundIcon}
                        tooltip={translate("Unsquare.ToolWindow.FitToGround",
                            "Fit the bottom corners to the original ground")}
                        onSelect={() => trigger(group, "fitToGround")}
                    />}
                />
                <div className={styles.divider} />
                <Section
                    title={translate("Unsquare.ToolWindow.Section.Align", "Align to neighbour")}
                    columns
                    top={<>
                        {alignSides.map(({ side, icon, fallback }) => (
                            <ActionButton
                                key={side}
                                icon={icon}
                                tooltip={translate(`Unsquare.ToolWindow.Align${side}`, fallback)}
                                onSelect={() => trigger(group, "alignToNeighbour", side)}
                            />
                        ))}
                        <div className={styles.spacer} />
                        <SegmentedToggle
                            options={toOptions(alignModes, "Unsquare.ToolWindow.AlignMode", translate)}
                            value={alignMode}
                            onSelect={mode => trigger(group, "selectAlignMode", mode)}
                        />
                    </>}
                    bottom={groundSides.map(({ side, icon, fallback }) => (
                        <ActionButton
                            key={side}
                            icon={icon}
                            tooltip={translate(`Unsquare.ToolWindow.AlignGround${side}`, fallback)}
                            onSelect={() => trigger(group, "alignGroundToNeighbour", side)}
                        />
                    ))}
                />
                <div className={styles.divider} />
                <Section
                    title={translate("Unsquare.ToolWindow.Section.Reset", "Reset")}
                    top={<ActionButton
                        icon={resetHandleIcon}
                        tooltip={translate("Unsquare.ToolWindow.ResetHandle", "Reset the selected corner, edge or face")}
                        disabled={!hasHandle}
                        onSelect={() => trigger(group, "resetHandle")}
                    />}
                    bottom={<ActionButton
                        icon={resetBuildingIcon}
                        tooltip={translate("Unsquare.ToolWindow.ResetBuilding", "Reset the whole building")}
                        onSelect={() => trigger(group, "resetBuilding")}
                    />}
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
