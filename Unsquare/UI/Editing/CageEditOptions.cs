namespace Unsquare.UI.Editing
{
    /// <summary>Tunables of the edit session: nudge step sizes, mouse pick radius and far-click margin.</summary>
    public sealed class CageEditOptions
    {
        public static readonly CageEditOptions Default = new(0.1f, 0.5f, 2f, 12f, 0.05f);

        public CageEditOptions(float smallStep, float mediumStep, float largeStep, float pickRadiusPixels,
            float farClickMarginFraction)
        {
            SmallStep = smallStep;
            MediumStep = mediumStep;
            LargeStep = largeStep;
            PickRadiusPixels = pickRadiusPixels;
            FarClickMarginFraction = farClickMarginFraction;
        }

        public float SmallStep { get; }
        public float MediumStep { get; }
        public float LargeStep { get; }

        /// <summary>How close (in screen pixels) the mouse must be to an edge, corner or gizmo arrow.</summary>
        public float PickRadiusPixels { get; }

        /// <summary>
        /// A click outside the target's cage grown by this part of its largest size steps back (handle, then
        /// building); closer misses change nothing.
        /// </summary>
        public float FarClickMarginFraction { get; }

        public override string ToString() =>
            $"steps {SmallStep}/{MediumStep}/{LargeStep} m, pick radius {PickRadiusPixels} px, " +
            $"far-click margin {FarClickMarginFraction * 100f} %";
    }
}
