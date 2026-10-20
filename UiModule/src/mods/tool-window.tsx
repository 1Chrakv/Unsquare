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

// A labelled group of controls; the header sits above the row.
const Section = ({ title, children }: { title: string | null; children: ReactNode }) => (
    <div className={styles.section}>
        <div className={styles.sectionHeader}>{title}</div>
        <div className={styles.sectionBody}>{children}</div>
    </div>
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
                <Section title={translate("Unsquare.ToolWindow.Section.Pick", "Pick")}>
                    <SegmentedToggle
                        options={toOptions(pickKinds, "Unsquare.ToolWindow.", translate)}
                        value={pickKind}
                        onSelect={kind => trigger(group, "selectPickKind", kind)}
                    />
                </Section>
                <div className={styles.divider} />
                <Section title={translate("Unsquare.ToolWindow.Section.Terrain", "Terrain")}>
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
                        tooltip={translate("Unsquare.ToolWindow.FitToGround",
                            "Fit the bottom corners to the original ground")}
                        selected={false}
                        multiSelect={false}
                        disabled={false}
                        focusKey={focusDisabled}
                        onSelect={() => trigger(group, "fitToGround")}
                    />
                    <div className={styles.spacer} />
                    <SegmentedToggle
                        options={toOptions(groundFitModes, "Unsquare.ToolWindow.GroundFitMode", translate)}
                        value={groundFitMode}
                        onSelect={mode => trigger(group, "selectGroundFitMode", mode)}
                    />
                    <div className={styles.spacer} />
                    {groundSides.map(({ side, icon, fallback }) => (
                        <ToolButton
                            key={side}
                            src={icon}
                            tooltip={translate(`Unsquare.ToolWindow.AlignGround${side}`, fallback)}
                            selected={false}
                            multiSelect={false}
                            disabled={false}
                            focusKey={focusDisabled}
                            onSelect={() => trigger(group, "alignGroundToNeighbour", side)}
                        />
                    ))}
                </Section>
                <div className={styles.divider} />
                <Section title={translate("Unsquare.ToolWindow.Section.Align", "Align to neighbour")}>
                    {alignSides.map(({ side, icon, fallback }) => (
                        <ToolButton
                            key={side}
                            src={icon}
                            tooltip={translate(`Unsquare.ToolWindow.Align${side}`, fallback)}
                            selected={false}
                            multiSelect={false}
                            disabled={false}
                            focusKey={focusDisabled}
                            onSelect={() => trigger(group, "alignToNeighbour", side)}
                        />
                    ))}
                    <div className={styles.spacer} />
                    <SegmentedToggle
                        options={toOptions(alignModes, "Unsquare.ToolWindow.AlignMode", translate)}
                        value={alignMode}
                        onSelect={mode => trigger(group, "selectAlignMode", mode)}
                    />
                </Section>
                <div className={styles.divider} />
                <Section title={translate("Unsquare.ToolWindow.Section.Reset", "Reset")}>
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
                </Section>
            </div>
        </div>
    );
};

export const GuardedToolWindow = () => (
    <Guard name="tool window">
        <ToolWindow />
    </Guard>
);
