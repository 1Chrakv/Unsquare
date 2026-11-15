using Unity.Mathematics;
using Unsquare.Core.Cage;
using Unsquare.Core.Deformation;

namespace Unsquare.Core.Terrain
{
    /// <summary>
    /// Whether the building stands on its lot's ground: compares how far the building and lot cages move the ground
    /// plane under the building's columns. Linked cages move it equally, so they are always seated.
    /// </summary>
    public static class BuildingSeat
    {
        /// <summary>Above this the building counts as buried or floating; more than grid rounding can cause.</summary>
        public const float ToleranceMeters = 0.15f;

        /// <summary>Lot-cage ground height under each column of <paramref name="columnsBox"/> (GroundFit order).</summary>
        public static float[] TargetHeights(CageBox lotBox, CageOffsets lotOffsets, CageBox columnsBox)
        {
            var lot = new TrilinearCageDeformation(lotBox, lotOffsets);
            var points = GroundFit.GroundPoints3(columnsBox, CageOffsets.Identity);
            var heights = new float[points.Length];
            for (int i = 0; i < points.Length; i++)
                heights[i] = lot.Apply(points[i], out _).y;
            return heights;
        }

        /// <summary>Largest vertical gap between building and lot ground under the building's columns.</summary>
        public static float MaxOffGround(CageBox lotBox, CageBox buildingBox, DeformSpec spec)
        {
            if (spec.IsLinked)
                return 0f;
            var building = new TrilinearCageDeformation(lotBox, spec.BuildingOffsets);
            var lot = new TrilinearCageDeformation(lotBox, spec.LotOffsets);
            float max = 0f;
            foreach (var point in GroundFit.GroundPoints3(buildingBox, CageOffsets.Identity))
                max = math.max(max, math.abs(building.Apply(point, out _).y - lot.Apply(point, out _).y));
            return max;
        }

        public static bool IsOffGround(CageBox lotBox, CageBox buildingBox, DeformSpec spec) =>
            MaxOffGround(lotBox, buildingBox, spec) > ToleranceMeters;
    }
}
