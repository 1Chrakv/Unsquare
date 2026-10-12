using System;
using Unsquare.Core.Cage;

namespace Unsquare.Core.Terrain
{
    /// <summary>
    /// Plans the terrain under a deformed lot. The game flattens the footprint to one height, so the flat area is
    /// collapsed to the lot centre at its lift and the game interpolates the edge heights in between.
    /// </summary>
    public static class TerrainSlopePlanner
    {
        public static TerrainSlope Plan(CageBox lotBox, CageOffsets offsets)
        {
            if (offsets == null)
                throw new ArgumentNullException(nameof(offsets));
            if (offsets.IsIdentity)
                return TerrainSlope.Empty;

            float centre = new GroundSurface(lotBox, offsets).LiftAt(lotBox.Center.xz);
            var slope = new TerrainSlope(centre, GroundLift.FromCage(lotBox, offsets).Minus(centre));
            return slope.IsEmpty ? TerrainSlope.Empty : slope;
        }
    }
}
