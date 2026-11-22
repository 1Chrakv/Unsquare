import { bindValue, trigger, useValue } from "cs2/api";
import { useLocalization } from "cs2/l10n";
import { Button, Panel } from "cs2/ui";
import { Guard } from "mods/guard";
import styles from "./legacy-saves-dialog.module.scss";

// Group and names must match LegacySavesUISystem on the C# side.
const group = "Unsquare";

const pending$ = bindValue<boolean>(group, "legacySavesPending", false);
// "convert": old-format buildings, save this city; "archived": their shapes are only in the archive, restore them;
// "remaining": a converted save while old saves still need the legacy variants, finish converting them.
const kind$ = bindValue<string>(group, "legacySavesKind", "convert");
// Buildings (convert, archived) or legacy variants (remaining) the message names.
const count$ = bindValue<number>(group, "legacySavesCount", 0);
// Result of the retire or restore: empty before; a refusal unless done.
const result$ = bindValue<string>(group, "legacySavesResult", "");
const done$ = bindValue<boolean>(group, "legacySavesDone", false);
// After a retire: buildings of this city whose old shape failed to load and must be deformed again.
const warning$ = bindValue<string>(group, "legacySavesWarning", "");

// After loading a save tied to old saves: asks to convert (or confirm all are), or to restore archived shapes.
const LegacySavesDialog = () => {
    const pending = useValue(pending$);
    const count = useValue(count$);
    const kind = useValue(kind$);
    const result = useValue(result$);
    const done = useValue(done$);
    const warning = useValue(warning$);
    const { translate } = useLocalization();
    if (!pending || !Panel || !Button)
        return null;
    const text = (key: string, fallback: string) =>
        translate(`Unsquare.LegacySavesDialog.${key}`, fallback) ?? fallback;
    const close = () => trigger(group, "closeLegacySaves");
    const archived = kind === "archived";
    // Whole sentences, one block each: the game's UI stacks inline pieces, so the highlighted step is its own line.
    const [lead, step, follow] = archived
        ? [text("ArchivedMessage", "{COUNT} deformed buildings in this save use the old format, but their shapes " +
            "were archived after \"I've converted all my saves\". Restore them, then load this save again before " +
            "saving it."), "", ""]
        : kind === "remaining"
            ? [text("RemainingMessage", "This save already uses the new format, but Unsquare still rebuilds {COUNT} " +
                "deformed buildings for older saves at every start, which slows loading."),
                text("SaveOld", "Open and save each old save you want to keep."),
                text("RemainingFollow", "Then press \"I've converted all my saves\".")]
            : [text("Message", "{COUNT} deformed buildings in this save use the old format."),
                text("SaveNow", "Save this city now to convert it."),
                text("MessageFollow", "Do the same with every other old save you want to keep.")];
    const outcome = done && archived
        ? text("Restored", "Shapes restored. Load this save again to see them; don't save it before that.")
        : result;
    return (
        <div className={styles.anchor}>
            <Panel
                className={styles.panel}
                contentClassName={styles.content}
                header={text("Title", "Changes in how Unsquare saves its deformations")}
                onClose={close}
            >
                {!result && (
                    <div className={styles.message}>
                        <div>{lead.replace("{COUNT}", String(count))}</div>
                        {step && <div className={`${styles.next} ${styles.highlight}`}>{step}</div>}
                        {follow && <div className={styles.next}>{follow}</div>}
                    </div>
                )}
                {result && <div className={done ? styles.message : styles.problem}>{outcome}</div>}
                {result && warning && <div className={styles.problem}>{warning}</div>}
                <div className={styles.buttons}>
                    {result ? (
                        <Button className={styles.button} variant="primary" onSelect={close}>
                            {text("Close", "Close")}
                        </Button>
                    ) : archived ? (
                        <>
                            <Button className={styles.button} variant="primary"
                                onSelect={() => trigger(group, "restoreLegacySaves")}>
                                {text("Restore", "Restore shapes")}
                            </Button>
                            <Button className={styles.button} variant="flat" onSelect={close}>
                                {text("NotNow", "Not now")}
                            </Button>
                        </>
                    ) : (
                        <>
                            <Button className={styles.button} variant="primary"
                                onSelect={() => trigger(group, "retireLegacySaves")}>
                                {text("Converted", "I've converted all my saves")}
                            </Button>
                            <div className={styles.hint}>
                                {text("Hint", "Old saves that aren't converted then load without their deformed " +
                                    "buildings. Can be undone: Options → Unsquare → \"Restore shapes for old saves\".")}
                            </div>
                            <Button className={styles.button} variant="flat" onSelect={close}>
                                {text("KeepOld", "I still have old saves I need to keep")}
                            </Button>
                        </>
                    )}
                </div>
            </Panel>
        </div>
    );
};

export const GuardedLegacySavesDialog = () => (
    <Guard name="Old saves dialog">
        <LegacySavesDialog />
    </Guard>
);
