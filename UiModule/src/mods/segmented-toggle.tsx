import { Button } from "cs2/ui";
import { focusDisabled } from "mods/vanilla";
import styles from "./segmented-toggle.module.scss";

export interface SegmentOption {
    value: string;
    icon: string;
    tooltip: string;
}

interface SegmentedToggleProps {
    options: SegmentOption[];
    value: string;
    onSelect: (value: string) => void;
}

// Segment width in rem; the thumb slides by whole segments, so it must match .segment in the stylesheet.
const segmentRem = 36;

/** A pill with one icon per option and a thumb that slides to the active one; clicks select directly. */
export const SegmentedToggle = ({ options, value, onSelect }: SegmentedToggleProps) => {
    const index = options.findIndex(option => option.value === value);
    return (
        <div className={styles.track}>
            {index >= 0 && (
                <div className={styles.thumb} style={{ transform: `translateX(${index * segmentRem}rem)` }} />
            )}
            {options.map((option, i) => (
                <Button
                    key={option.value}
                    variant="icon"
                    src={option.icon}
                    tinted={false}
                    tooltipLabel={option.tooltip}
                    focusKey={focusDisabled}
                    selectSound="select-toggle"
                    theme={{ button: i === index ? styles.segmentActive : styles.segment, icon: styles.icon }}
                    onSelect={() => {
                        if (i !== index)
                            onSelect(option.value);
                    }}
                />
            ))}
        </div>
    );
};
