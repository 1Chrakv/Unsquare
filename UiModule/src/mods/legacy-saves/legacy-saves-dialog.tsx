import { useState } from "react";
import { bindValue, trigger, useValue } from "cs2/api";
import { useLocalization } from "cs2/l10n";
import { Button, Panel } from "cs2/ui";
import { Guard } from "mods/guard";
import styles from "./legacy-saves-dialog.module.scss";

// Group and names must match LegacySavesUISystem on the C# side.
const group = "Unsquare";

const pending$ = bindValue<boolean>(group, "legacySavesPending", false);
// Deformed buildings of the loaded save still in the format of saves from before 1.2.
const count$ = bindValue<number>(group, "legacySavesCount", 0);
// Result of "I've converted all my saves": empty before; a refusal unless done.
const result$ = bindValue<string>(group, "legacySavesResult", "");
const done$ = bindValue<boolean>(group, "legacySavesDone", false);

// After loading an old save: asks to resave old saves, then to confirm when all are converted.
const LegacySavesDialog = () => {
    const pending = useValue(pending$);
    const count = useValue(count$);
    const result = useValue(result$);
    const done = useValue(done$);
    const [confirming, setConfirming] = useState(false);
    const { translate } = useLocalization();
    if (!pending || !Panel || !Button)
        return null;
    const text = (key: string, fallback: string) =>
        translate(`Unsquare.LegacySavesDialog.${key}`, fallback) ?? fallback;
    const close = () => {
        setConfirming(false);
        trigger(group, "closeLegacySaves");
    };
    const retire = () => {
        setConfirming(false);
        trigger(group, "retireLegacySaves");
    };
    return (
        <div className={styles.anchor}>
            <Panel
                className={styles.panel}
                contentClassName={styles.content}
                header={text("Title", "Saves from an older Unsquare version")}
                onClose={close}
            >
                <div className={styles.message}>
                    {text("Message", "{COUNT} deformed buildings in this save use the old format, which Unsquare " +
                        "rebuilds at every start and which makes loading slower. Save this city over the same file " +
                        "to convert it, and do the same with every other old save you want to keep. Saving under a " +
                        "new name leaves the original old.").replace("{COUNT}", String(count))}
                </div>
                {!result && (
                    <div className={styles.message}>
                        {confirming
                            ? text("ConfirmText", "Save this city over the same file before you quit, so it is " +
                                "converted too. Old saves you did not convert lose their deformed buildings until " +
                                "you restore them in the options.")
                            : text("Hint", "When all of them are converted, press \"I've converted all my saves\": " +
                                "from the next start, old saves are no longer rebuilt. Options → Unsquare can bring " +
                                "them back.")}
                    </div>
                )}
                {result && (
                    <div className={done ? styles.message : styles.problem}>{result}</div>
                )}
                <div className={styles.buttons}>
                    {result ? (
                        <Button className={styles.button} variant="primary" onSelect={close}>
                            {text("Close", "Close")}
                        </Button>
                    ) : confirming ? (
                        <>
                            <Button className={styles.button} variant="primary" onSelect={retire}>
                                {text("Confirm", "Yes, all my saves are converted")}
                            </Button>
                            <Button className={styles.button} variant="flat" onSelect={() => setConfirming(false)}>
                                {text("Back", "Back")}
                            </Button>
                        </>
                    ) : (
                        <>
                            <Button className={styles.button} variant="primary" onSelect={close}>
                                {text("GotIt", "Got it")}
                            </Button>
                            <Button className={styles.button} variant="flat" onSelect={() => setConfirming(true)}>
                                {text("Converted", "I've converted all my saves")}
                            </Button>
                            <Button className={styles.button} variant="flat"
                                onSelect={() => trigger(group, "hideLegacySaves")}>
                                {text("Hide", "Don't show again")}
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
