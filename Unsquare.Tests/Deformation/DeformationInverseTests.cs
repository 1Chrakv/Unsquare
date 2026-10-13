using NUnit.Framework;
using Unity.Mathematics;
using Unsquare.Core.Cage;
using Unsquare.Core.Deformation;

namespace Unsquare.Tests.Deformation
{
    public class DeformationInverseTests
    {
        private static readonly CageBox Box = new(new float3(-16, 0, -12), new float3(16, 8, 12));

        internal static TrilinearCageDeformation Skewed() => new(Box, CageOffsets.Identity
            .Move(CageHandle.Corner(0), new float3(-4f, 0f, 3f))
            .Move(CageHandle.Corner(5), new float3(6f, 0f, -2f))
            .Move(CageHandle.Corner(7), new float3(2f, 3f, -1f))
            .Move(CageHandle.Edge(9), new float3(0f, -0.8f, 0.4f)));

        private static readonly float3[] Samples =
        {
            new(0, 0, 0), new(-15, 0, 11), new(14, 7, -10), new(3.3f, 2.2f, -7.7f), new(18, 0, 13), new(-17, 9, -1),
        };

        [Test]
        public void TryInvert_SkewedCage_RoundTrips()
        {
            var d = Skewed();
            foreach (var p in Samples)
            {
                Assert.That(DeformationInverse.TryInvert(d, d.Apply(p, out _), out var back), Is.True, $"{p}");
                Assert.That(TrilinearCageDeformationTests.Near(back, p, 1e-3f), Is.True, $"{p} -> {back}");
            }
        }

        [Test]
        public void TryInvert_Identity_ReturnsTarget()
        {
            var d = new TrilinearCageDeformation(Box, CageOffsets.Identity);
            var p = new float3(1.5f, 2f, -3f);
            Assert.That(DeformationInverse.TryInvert(d, p, out var back), Is.True);
            Assert.That(TrilinearCageDeformationTests.Near(back, p), Is.True);
        }

        [Test]
        public void TryInvertAtHeight_SkewedCage_RoundTripsGroundPoints()
        {
            var d = Skewed();
            foreach (var p in Samples)
            {
                var ground = new float3(p.x, 0f, p.z);
                float2 target = d.Apply(ground, out _).xz;
                Assert.That(DeformationInverse.TryInvertAtHeight(d, target, 0f, out var xz), Is.True, $"{p}");
                Assert.That(math.all(math.abs(xz - ground.xz) <= 1e-3f), Is.True, $"{ground.xz} -> {xz}");
            }
        }
    }
}
