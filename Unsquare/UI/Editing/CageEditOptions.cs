using Unsquare.Core.Cage;

namespace Unsquare.UI.Editing
{
    /// <summary>Tunables of the edit session: scope for undeformed buildings and the three nudge step sizes.</summary>
    public sealed class CageEditOptions
    {
        public static readonly CageEditOptions Default = new(CageScope.Lot, 0.1f, 0.5f, 2f);

        public CageEditOptions(CageScope defaultScope, float smallStep, float mediumStep, float largeStep)
        {
            DefaultScope = defaultScope;
            SmallStep = smallStep;
            MediumStep = mediumStep;
            LargeStep = largeStep;
        }

        /// <summary>Scope for buildings that are not deformed yet; objects without a lot fall back to Building.</summary>
        public CageScope DefaultScope { get; }

        public float SmallStep { get; }
        public float MediumStep { get; }
        public float LargeStep { get; }

        public override string ToString() =>
            $"default scope {DefaultScope}, steps {SmallStep}/{MediumStep}/{LargeStep} m";
    }
}
