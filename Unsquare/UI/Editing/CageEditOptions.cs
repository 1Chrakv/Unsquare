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

        public override string ToString() =>
            $"steps {SmallStep}/{MediumStep}/{LargeStep} m, pick radius {PickRadiusPixels} px";
    }
}
