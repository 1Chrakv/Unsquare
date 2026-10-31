using Unity.Mathematics;
using Unsquare.Core.Deformation;

namespace Unsquare.Core.Cage
{
    /// <summary>
    /// A larger cage around a building and its on-lot upgrades. Trilinear deformation continued outside its box is the
    /// trilinear deformation of any larger box, so both cages describe the same shape and convert exactly.
    /// </summary>
    public static class ExtendedCage
    {
        /// <summary>The box with its top raised to <paramref name="top"/>; unchanged if already that tall.</summary>
        public static CageBox RaiseTop(CageBox box, float top) =>
            top <= box.Max.y ? box : new CageBox(box.Min, new float3(box.Max.x, top, box.Max.z));

        /// <summary>Offsets of <paramref name="toBox"/> that deform space like <paramref name="fromOffsets"/> on
        /// <paramref name="fromBox"/>, rounded to the offset grid.</summary>
        public static CageOffsets Convert(CageBox fromBox, CageOffsets fromOffsets, CageBox toBox)
        {
            if (fromBox.Equals(toBox) || fromOffsets.IsIdentity)
                return fromOffsets;
            var deformation = new TrilinearCageDeformation(fromBox, fromOffsets);
            var meters = new float3[CageTopology.CornerCount];
            for (int i = 0; i < meters.Length; i++)
            {
                float3 corner = toBox.Corner(i);
                meters[i] = deformation.Apply(corner, out _) - corner;
            }
            return CageOffsets.FromMeters(meters);
        }
    }
}
