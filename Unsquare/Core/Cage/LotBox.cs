using System;
using Unity.Mathematics;
using Unsquare.Core.Deformation;

namespace Unsquare.Core.Cage
{
    /// <summary>Cage box of a whole lot: the lot rectangle (centred on the building) from ground to roof.</summary>
    public static class LotBox
    {
        /// <summary>Zone cell size in meters; a lot is cells × 8 m around the building origin.</summary>
        public const float CellSize = 8f;

        /// <summary>Minimum box height (one cell): flat lots like parks need vertical edges long enough to pick.</summary>
        public const float MinHeight = 8f;

        public static CageBox FromLot(int2 lotCells, MinMaxBounds meshBounds)
        {
            if (math.any(lotCells <= 0))
                throw new ArgumentException($"Lot size must be positive: {lotCells}.", nameof(lotCells));

            float2 half = (float2)lotCells * (CellSize * 0.5f);
            float bottom = meshBounds.IsEmpty ? 0f : math.min(meshBounds.Min.y, 0f);
            float top = meshBounds.IsEmpty ? MinHeight : math.max(meshBounds.Max.y, MinHeight);
            return new CageBox(new float3(-half.x, bottom, -half.y), new float3(half.x, top, half.y));
        }
    }
}
