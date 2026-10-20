using NUnit.Framework;
using Unity.Mathematics;
using Unsquare.Core.Cage;
using Unsquare.Core.Deformation;

namespace Unsquare.Tests.Deformation
{
    public class CageTwistTests
    {
        [Test]
        public void MaxResidualMeters_Identity_IsZero()
        {
            Assert.That(CageTwist.MaxResidualMeters(CageOffsets.Identity), Is.EqualTo(0f));
        }

        [Test]
        public void MaxResidualMeters_UniformMove_IsZero()
        {
            Assert.That(CageTwist.MaxResidualMeters(FromCorners(_ => new float3(1.5f, -0.3f, 2f))),
                Is.EqualTo(0f).Within(1e-5f));
        }

        [Test]
        public void MaxResidualMeters_StretchAndShear_IsZero()
        {
            // Affine in the corner position: stretch x by 2 m, shear z by 1 m per unit height.
            var offsets = FromCorners(c => new float3(c.x * 2f, 0f, c.y * 1f));
            Assert.That(CageTwist.MaxResidualMeters(offsets), Is.EqualTo(0f).Within(1e-5f));
        }

        [Test]
        public void MaxResidualMeters_SingleCornerMoved_IsHalfTheMove()
        {
            var offsets = FromCorners(c => c.Equals(new int3(1, 1, 1)) ? new float3(0f, 2f, 0f) : float3.zero);
            Assert.That(CageTwist.MaxResidualMeters(offsets), Is.EqualTo(1f).Within(1e-5f));
        }

        [Test]
        public void MaxResidualMeters_OppositeRoofCornersRaised_IsHalfTheRaise()
        {
            var offsets = FromCorners(c => c.y == 1 && c.x == c.z ? new float3(0f, 3f, 0f) : float3.zero);
            Assert.That(CageTwist.MaxResidualMeters(offsets), Is.EqualTo(1.5f).Within(1e-5f));
        }

        private static CageOffsets FromCorners(System.Func<int3, float3> offsetOf)
        {
            var meters = new float3[CageTopology.CornerCount];
            for (int i = 0; i < meters.Length; i++)
                meters[i] = offsetOf(new int3(i & 1, (i >> 1) & 1, (i >> 2) & 1));
            return CageOffsets.FromMeters(meters);
        }
    }
}
