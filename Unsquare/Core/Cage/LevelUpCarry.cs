using System;
using Unity.Mathematics;

namespace Unsquare.Core.Cage
{
    /// <summary>
    /// Carries a deformation onto the building a growable levels up to: same lot, other height. Bottom corners stay;
    /// each column's top keeps its offset relative to the bottom, scaled by the height ratio.
    /// </summary>
    public static class LevelUpCarry
    {
        private const int TopBit = 2;

        public static DeformSpec Rescale(DeformSpec spec, CageBox fromLot, CageBox toLot)
        {
            if (spec == null)
                throw new ArgumentNullException(nameof(spec));
            float ratio = toLot.Size.y / fromLot.Size.y;
            var lot = Rescale(spec.LotOffsets, ratio);
            // Linked cages must stay one cage, not two equal copies that drift apart in rounding.
            var building = spec.IsLinked ? lot : Rescale(spec.BuildingOffsets, ratio);
            return new DeformSpec(lot, building, spec.FlatTerrain, spec.BlendMode);
        }

        private static CageOffsets Rescale(CageOffsets offsets, float ratio)
        {
            var result = offsets;
            for (int corner = 0; corner < CageTopology.CornerCount; corner++)
            {
                if ((corner & TopBit) == 0)
                    continue;
                int3 bottom = offsets.Steps(corner & ~TopBit);
                int3 rise = offsets.Steps(corner) - bottom;
                result = result.WithCornerSteps(corner, bottom + (int3)math.round((float3)rise * ratio));
            }
            return result;
        }
    }
}
