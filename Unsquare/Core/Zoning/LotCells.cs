using System;
using Unity.Mathematics;

namespace Unsquare.Core.Zoning
{
    /// <summary>
    /// Zone cells under a building's lot in world xz. Cells are numbered like the game's zone check: x runs from the
    /// lot's +X side to −X, y from the front (+Z) to the back.
    /// </summary>
    public static class LotCells
    {
        public const float CellSize = 8f;

        public static float2 CellCenter(float2 position, quaternion rotation, int2 lotSize, int2 cell)
        {
            if (math.any(lotSize < 1))
                throw new ArgumentException("Lot size must be positive.", nameof(lotSize));
            float2 local = ((float2)(lotSize - 1) * 0.5f - cell) * CellSize;
            return position + math.rotate(rotation, new float3(local.x, 0f, local.y)).xz;
        }

        /// <summary>Axis-aligned world xz bounds of the rotated lot rectangle.</summary>
        public static void Bounds(float2 position, quaternion rotation, int2 lotSize, out float2 min, out float2 max)
        {
            if (math.any(lotSize < 1))
                throw new ArgumentException("Lot size must be positive.", nameof(lotSize));
            float2 right = math.rotate(rotation, new float3(1f, 0f, 0f)).xz * (lotSize.x * CellSize * 0.5f);
            float2 forward = math.rotate(rotation, new float3(0f, 0f, 1f)).xz * (lotSize.y * CellSize * 0.5f);
            float2 half = math.abs(right) + math.abs(forward);
            min = position - half;
            max = position + half;
        }
    }
}
