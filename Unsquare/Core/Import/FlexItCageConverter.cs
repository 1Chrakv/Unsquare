using Unity.Mathematics;
using Unsquare.Core.Cage;

namespace Unsquare.Core.Import
{
    /// <summary>
    /// Expresses a FlexIt taper and shear as lot cage offsets. Both are bilinear in plan and keep heights, so mapping
    /// the 8 lot corners reproduces them exactly (up to the offset grid); top and bottom corners move alike.
    /// </summary>
    public static class FlexItCageConverter
    {
        public static CageOffsets ToOffsets(CageBox lotBox, FlexItTaper? taper, FlexItShear? shear)
        {
            var meters = new float3[CageTopology.CornerCount];
            for (int i = 0; i < meters.Length; i++)
            {
                float3 corner = lotBox.Corner(i);
                float2 xz = corner.xz;
                if (taper.HasValue)
                    xz = taper.Value.Map(xz);
                if (shear.HasValue)
                    xz = shear.Value.Map(xz);
                meters[i] = new float3(xz.x - corner.x, 0f, xz.y - corner.z);
            }
            return CageOffsets.FromMeters(meters);
        }
    }
}
