namespace Unsquare.UI.Editing
{
    /// <summary>Tunables of the edit session: the three nudge step sizes.</summary>
    public sealed class CageEditOptions
    {
        public static readonly CageEditOptions Default = new(0.1f, 0.5f, 2f);

        public CageEditOptions(float smallStep, float mediumStep, float largeStep)
        {
            SmallStep = smallStep;
            MediumStep = mediumStep;
            LargeStep = largeStep;
        }

        public float SmallStep { get; }
        public float MediumStep { get; }
        public float LargeStep { get; }

        public override string ToString() => $"steps {SmallStep}/{MediumStep}/{LargeStep} m";
    }
}
