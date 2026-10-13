using NUnit.Framework;
using Unity.Mathematics;
using Unsquare.Core.Cage;
using Unsquare.Core.Deformation;

namespace Unsquare.Tests.Deformation
{
    public class LotRemapTests
    {
        private static readonly CageBox Box = new(new float3(-16, 0, -12), new float3(16, 8, 12));
        private static readonly float3 Point = new(5f, 3f, -4f);

        private static TrilinearCageDeformation Other() => new(Box, CageOffsets.Identity
            .Move(CageHandle.Corner(3), new float3(3f, 0f, 2f))
            .Move(CageHandle.Face(3), new float3(0f, 4f, 0f)));

        [Test]
        public void TryRemap_FromBase_AppliesNewDeformation()
        {
            var to = DeformationInverseTests.Skewed();
            Assert.That(new LotRemap(null, to).TryRemap(Point, false, out var result), Is.True);
            Assert.That(TrilinearCageDeformationTests.Near(result, to.Apply(Point, out _), 1e-3f), Is.True);
        }

        [Test]
        public void TryRemap_ToBase_RestoresOriginal()
        {
            var from = DeformationInverseTests.Skewed();
            Assert.That(new LotRemap(from, null).TryRemap(from.Apply(Point, out _), false, out var result), Is.True);
            Assert.That(TrilinearCageDeformationTests.Near(result, Point, 1e-3f), Is.True);
        }

        [Test]
        public void TryRemap_BetweenVariants_EqualsNewOfOriginal()
        {
            var from = DeformationInverseTests.Skewed();
            var to = Other();
            Assert.That(new LotRemap(from, to).TryRemap(from.Apply(Point, out _), false, out var result), Is.True);
            Assert.That(TrilinearCageDeformationTests.Near(result, to.Apply(Point, out _), 2e-3f), Is.True);
        }

        [Test]
        public void TryRemap_OnGround_KeepsHeightAndMapsAtGroundPlane()
        {
            var to = DeformationInverseTests.Skewed();
            var ground = new float3(Point.x, 1.7f, Point.z);
            Assert.That(new LotRemap(null, to).TryRemap(ground, true, out var result), Is.True);
            float2 expected = to.Apply(new float3(Point.x, LotRemap.GroundY, Point.z), out _).xz;
            Assert.That(result.y, Is.EqualTo(1.7f));
            Assert.That(math.all(math.abs(result.xz - expected) <= 1e-4f), Is.True);
        }

        [Test]
        public void TryRemapAlong_StreetEdgeAlongX_FollowsCageXAndKeepsDepthAndHeight()
        {
            var to = DeformationInverseTests.Skewed();
            var end = new float3(5f, 0.4f, 12.5f); // just beyond the street edge (z = 12)
            Assert.That(new LotRemap(null, to).TryRemapAlong(end, new float2(1f, 0f), out var result), Is.True);
            float expectedX = to.Apply(new float3(end.x, LotRemap.GroundY, end.z), out _).x;
            Assert.That(result.x, Is.EqualTo(expectedX).Within(1e-4f));
            Assert.That(result.y, Is.EqualTo(0.4f));
            Assert.That(result.z, Is.EqualTo(12.5f));
        }

        [Test]
        public void TryRemapAlong_DeformThenReset_ReturnsOriginal()
        {
            var d = DeformationInverseTests.Skewed();
            var end = new float3(-7f, 0f, 11f);
            var road = new float2(0.8f, 0.6f);
            Assert.That(new LotRemap(null, d).TryRemapAlong(end, road, out var deformed), Is.True);
            Assert.That(new LotRemap(d, null).TryRemapAlong(deformed, road, out var back), Is.True);
            Assert.That(TrilinearCageDeformationTests.Near(back, end, 1e-3f), Is.True, $"{back}");
        }

        [Test]
        public void TryRemapAlong_KeepsDistanceAcrossDirection()
        {
            var road = math.normalize(new float2(0.8f, 0.6f));
            var normal = new float2(-road.y, road.x);
            var end = new float3(-7f, 0f, 11f);
            Assert.That(new LotRemap(null, DeformationInverseTests.Skewed()).TryRemapAlong(end, road, out var result),
                Is.True);
            Assert.That(math.dot(normal, result.xz), Is.EqualTo(math.dot(normal, end.xz)).Within(1e-4f));
        }

        [Test]
        public void TryRemap_Curve_RoundTripsThroughDeformAndReset()
        {
            var d = DeformationInverseTests.Skewed();
            var curve = new CubicCurve(new float3(-10, 0, -5), new float3(-4, 0, 2), new float3(3, 0, 6), new float3(9, 0, 1));
            Assert.That(new LotRemap(null, d).TryRemap(curve, true, out var deformed), Is.True);
            Assert.That(new LotRemap(d, null).TryRemap(deformed, true, out var back), Is.True);
            Assert.That(TrilinearCageDeformationTests.Near(back.A, curve.A, 1e-3f) && TrilinearCageDeformationTests.Near(back.B, curve.B, 1e-3f) &&
                        TrilinearCageDeformationTests.Near(back.C, curve.C, 1e-3f) && TrilinearCageDeformationTests.Near(back.D, curve.D, 1e-3f), Is.True);
        }
    }
}
