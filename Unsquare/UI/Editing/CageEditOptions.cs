namespace Unsquare.UI.Editing
{
    /// <summary>Tunables of the edit session: the three nudge step sizes and the mouse pick radius.</summary>
    public sealed class CageEditOptions
    {
        public static readonly CageEditOptions Default = new(0.1f, 0.5f, 2f, 12f);

        public CageEditOptions(float smallStep, float mediumStep, float largeStep, float pickRadiusPixels)
        {
            SmallStep = smallStep;
            MediumStep = mediumStep;
            LargeStep = largeStep;
            PickRadiusPixels = pickRadiusPixels;
        }

        public float SmallStep { get; }
        public float MediumStep { get; }
        public float LargeStep { get; }

        /// <summary>How close (in screen pixels) the mouse must be to an edge, corner or gizmo arrow.</summary>
        public float PickRadiusPixels { get; }

        /// <summary>
        /// A click outside the cage grown by this margin (part of its largest size, at least the minimum in metres)
        /// deselects the handle; closer misses keep it.
        /// </summary>
        public float FarClickMarginFraction { get; } = 0.25f;
        public float FarClickMinMargin { get; } = 5f;

        public override string ToString() =>
            $"steps {SmallStep}/{MediumStep}/{LargeStep} m, pick radius {PickRadiusPixels} px";
    }
}
