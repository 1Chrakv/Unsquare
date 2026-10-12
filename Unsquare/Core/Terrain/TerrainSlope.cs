using System;

namespace Unsquare.Core.Terrain
{
    /// <summary>
    /// Terrain of a deformed lot: the pad (collapsed to the lot centre) raised by <see cref="PadOffset"/>, and the lot
    /// edge heights relative to it, which the game interpolates across the lot.
    /// </summary>
    public sealed class TerrainSlope
    {
        public static readonly TerrainSlope Empty = new(0f, LotEdgeLift.Zero);

        public TerrainSlope(float padOffset, LotEdgeLift edgeLift)
        {
            PadOffset = padOffset;
            EdgeLift = edgeLift;
        }

        public float PadOffset { get; }
        public LotEdgeLift EdgeLift { get; }

        public bool IsEmpty => Math.Abs(PadOffset) < LotEdgeLift.MinMeters && EdgeLift.IsZero;

        public override string ToString() => $"pad {PadOffset:0.##} m, edges {EdgeLift}";
    }
}
