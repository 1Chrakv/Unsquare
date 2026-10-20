using System;

namespace Unsquare.Core.Editing
{
    /// <summary>Which edges count as the neighbour of a lot face (see <see cref="NeighbourAlign"/>).</summary>
    public sealed class NeighbourAlignOptions
    {
        public static NeighbourAlignOptions Default { get; } = new(200f, 45f);

        /// <param name="searchMeters">How far outside the face a neighbour edge may be.</param>
        /// <param name="maxAngleDegrees">How far a neighbour edge may be turned against the face.</param>
        public NeighbourAlignOptions(float searchMeters, float maxAngleDegrees)
        {
            if (searchMeters <= 0f)
                throw new ArgumentException("Search distance must be positive.", nameof(searchMeters));
            if (maxAngleDegrees <= 0f || maxAngleDegrees >= 90f)
                throw new ArgumentException("Angle must be between 0 and 90 degrees.", nameof(maxAngleDegrees));
            SearchMeters = searchMeters;
            MaxAngleDegrees = maxAngleDegrees;
        }

        public float SearchMeters { get; }
        public float MaxAngleDegrees { get; }
    }
}
