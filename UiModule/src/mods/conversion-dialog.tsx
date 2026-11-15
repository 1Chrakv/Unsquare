import { bindValue, trigger, useValue } from "cs2/api";
import { useLocalization } from "cs2/l10n";
import { Button, Panel } from "cs2/ui";
import { Guard } from "mods/guard";
import styles from "./conversion-dialog.module.scss";

// Group and names must match UnsquareUISystem on the C# side.
const group = "Unsquare";

const conversionPending$ = bindValue<boolean>(group, "conversionPending", false);
// Why converting failed: a PlacemakerConversionResult name, empty while nothing failed.
const conversionProblem$ = bindValue<string>(group, "conversionProblem", "");
// Houses in the picked building's Placemaker row (itself included); 2 or more offers converting the row.
const conversionRowSize$ = bindValue<number>(group, "conversionRowSize", 0);

const problemFallbacks: Record<string, string> = {
    PlacemakerBusy: "Placemaker is reshaping this building right now. Try again in a moment.",
    PlacemakerMissing: "Placemaker could not be read (not running or changed). The building stays as it is.",
    Failed: "The building could not be converted. See Unsquare.Mod.log.",
};

// Asks what to do with a building Placemaker has bent, before Unsquare may edit it.
const ConversionDialog = () => {
    const pending = useValue(conversionPending$);
    const problem = useValue(conversionProblem$);
    const rowSize = useValue(conversionRowSize$);
    const { translate } = useLocalization();
    if (!pending || !Panel || !Button)
        return null;
    const text = (key: string, fallback: string) => translate(`Unsquare.ConvertDialog.${key}`, fallback) ?? fallback;
    return (
        <div className={styles.anchor}>
            <Panel
                className={styles.panel}
                contentClassName={styles.content}
                header={text("Title", "Building shaped by Placemaker")}
                onClose={() => trigger(group, "declineConversion")}
            >
                <div className={styles.message}>
                    {text("Message", "Placemaker has bent this building to fit its row. Convert it to an Unsquare " +
                        "cage of the same shape? Placemaker then leaves it alone; Reset gives the plain building " +
                        "back.")}
                </div>
                {problem && (
                    <div className={styles.problem}>
                        {text(`Problem.${problem}`, problemFallbacks[problem] ?? problemFallbacks.Failed)}
                    </div>
                )}
                <div className={styles.buttons}>
                    {rowSize > 1 && (
                        <>
                            <Button className={styles.button} variant="primary"
                                onSelect={() => trigger(group, "confirmRowConversion")}>
                                {text("ConvertRow", "Convert whole row ({COUNT} houses)")
                                    .replace("{COUNT}", String(rowSize))}
                            </Button>
                            <div className={styles.warning}>
                                {text("RowHint", "Placemaker straightens the neighbours of a house it no longer " +
                                    "shapes. Convert the whole row to keep it joined.")}
                            </div>
                        </>
                    )}
                    <Button className={styles.button} variant={rowSize > 1 ? "flat" : "primary"}
                        onSelect={() => trigger(group, "confirmConversion")}>
                        {text("Convert", "Convert to Unsquare")}
                    </Button>
                    <Button className={styles.button} variant="flat"
                        onSelect={() => trigger(group, "deformOnTop")}>
                        {text("OnTop", "Deform on top of Placemaker")}
                    </Button>
                    <div className={styles.warning}>
                        {text("OnTopWarning", "Keeps Placemaker's model as the base. Such buildings may not survive " +
                            "reloading the save.")}
                    </div>
                    <Button className={styles.button} variant="flat"
                        onSelect={() => trigger(group, "declineConversion")}>
                        {text("Cancel", "Cancel")}
                    </Button>
                </div>
            </Panel>
        </div>
    );
};

export const GuardedConversionDialog = () => (
    <Guard name="conversion dialog">
        <ConversionDialog />
    </Guard>
);
