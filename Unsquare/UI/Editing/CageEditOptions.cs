namespace Unsquare.UI.Editing
{
    /// <summary>
    /// Tunables of the edit session: nudge step sizes, mouse pick radius, drag threshold, far-click margin, undo depth.
    /// </summary>
    public sealed class CageEditOptions
    {
        public static readonly CageEditOptions Default = new(0.1f, 0.5f, 2f, 12f, 4f, 0.05f, 100);

        public CageEditOptions(float smallStep, float mediumStep, float largeStep, float pickRadiusPixels,
            float dragThresholdPixels, float farClickMarginFraction, int historyDepth)
        {
            SmallStep = smallStep;
            MediumStep = mediumStep;
            LargeStep = largeStep;
            PickRadiusPixels = pickRadiusPixels;
            DragThresholdPixels = dragThresholdPixels;
            FarClickMarginFraction = farClickMarginFraction;
            HistoryDepth = historyDepth;
        }

        public float SmallStep { get; }
        public float MediumStep { get; }
        public float LargeStep { get; }

        /// <summary>How close (in screen pixels) the mouse must be to an edge, corner or gizmo arrow.</summary>
        public float PickRadiusPixels { get; }

        /// <summary>How far (in screen pixels) the mouse must move after pressing on a handle to drag it.</summary>
        public float DragThresholdPixels { get; }

        /// <summary>
        /// A click outside the target's cage grown by this part of its largest size steps back (handle, then
        /// building); closer misses change nothing.
        /// </summary>
        public float FarClickMarginFraction { get; }

        /// <summary>How many cage edits Ctrl+Z can take back.</summary>
        public int HistoryDepth { get; }

        public override string ToString() =>
            $"steps {SmallStep}/{MediumStep}/{LargeStep} m, pick radius {PickRadiusPixels} px, " +
            $"drag threshold {DragThresholdPixels} px, " +
            $"far-click margin {FarClickMarginFraction * 100f} %, {HistoryDepth} undo steps";
    }
}
