import { useLayoutEffect, useRef, useState } from "react";
import { focusDisabled, ToolButton } from "mods/vanilla";
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
    // Shows the current option but ignores clicks, e.g. while the setting doesn't apply.
    disabled?: boolean;
}

interface ThumbBox {
    left: number;
    top: number;
    width: number;
    height: number;
}

/**
 * A rounded track with one tool button per option and a thumb that slides to the active one; clicks select directly.
 * The thumb is measured from the active button, so it fits whatever size the game gives tool buttons.
 */
export const SegmentedToggle = ({ options, value, onSelect, disabled }: SegmentedToggleProps) => {
    const index = options.findIndex(option => option.value === value);
    const segments = useRef<(HTMLDivElement | null)[]>([]);
    const [thumb, setThumb] = useState<ThumbBox | null>(null);

    useLayoutEffect(() => {
        let frame = 0;
        const measure = () => {
            const segment = index >= 0 ? segments.current[index] : null;
            // The UI may not have laid the buttons out yet on the first frame; measure again on the next one.
            if (segment && segment.offsetWidth === 0) {
                frame = requestAnimationFrame(measure);
                return;
            }
            setThumb(segment
                ? { left: segment.offsetLeft, top: segment.offsetTop, width: segment.offsetWidth, height: segment.offsetHeight }
                : null);
        };
        measure();
        return () => cancelAnimationFrame(frame);
    }, [index, options.length]);

    return (
        <div className={disabled ? `${styles.track} ${styles.trackDisabled}` : styles.track}>
            {thumb && (
                <div
                    className={styles.thumb}
                    style={{
                        transform: `translateX(${thumb.left}px)`,
                        top: `${thumb.top}px`,
                        width: `${thumb.width}px`,
                        height: `${thumb.height}px`,
                    }}
                />
            )}
            {options.map((option, i) => (
                <div
                    key={option.value}
                    ref={element => { segments.current[i] = element; }}
                    className={i === index ? styles.segmentActive : styles.segment}
                >
                    <ToolButton
                        src={option.icon}
                        tooltip={option.tooltip}
                        selected={false}
                        multiSelect={false}
                        disabled={disabled ?? false}
                        focusKey={focusDisabled}
                        onSelect={() => {
                            if (!disabled && i !== index)
                                onSelect(option.value);
                        }}
                    />
                </div>
            ))}
        </div>
    );
};
