using NUnit.Framework;
using Unity.Mathematics;
using Unsquare.Core.Cage;
using Unsquare.Core.Deformation;

namespace Unsquare.Tests.Deformation
{
    public class CurveAndPolygonTests
    {
        private static readonly CageBox Box = new(new float3(-10, 0, -10), new float3(10, 5, 10));

        private static bool Near(float3 a, float3 b) => math.all(math.abs(a - b) <= 1e-4f);

        private static readonly CubicCurve Fence = new(
            new float3(-8, 0, -8), new float3(-4, 0, -8), new float3(0, 0, -8), new float3(4, 0, -8));

        [Test]
        public void Deform_Translation_MovesAllControlPoints()
        {
            var meters = new float3[8];
            for (int i = 0; i < meters.Length; i++)
                meters[i] = new float3(1.5f, 0, -2);
            var curve = CurveDeformer.Deform(new TrilinearCageDeformation(Box, CageOffsets.FromMeters(meters)), Fence);

            Assert.That(Near(curve.A, Fence.A + new float3(1.5f, 0, -2)), Is.True);
            Assert.That(Near(curve.C, Fence.C + new float3(1.5f, 0, -2)), Is.True);
        }

        [Test]
        public void Deform_EndpointsFollowDeformation()
        {
            var offsets = CageOffsets.Identity.Move(CageHandle.Face(1), new float3(4, 0, 0));
            var deformation = new TrilinearCageDeformation(Box, offsets);
            var curve = CurveDeformer.Deform(deformation, Fence);

            Assert.That(Near(curve.A, deformation.Apply(Fence.A, out _)), Is.True);
            Assert.That(Near(curve.D, deformation.Apply(Fence.D, out _)), Is.True);
        }

        [Test]
        public void Reversed_SwapsControlPoints()
        {
            var reversed = Fence.Reversed();
            Assert.That(Near(reversed.A, Fence.D) && Near(reversed.B, Fence.C), Is.True);
        }

        [Test]
        public void IsSimple_SquareAndConcave_AreSimple()
        {
            Assert.That(PolygonCheck.IsSimple(new[] { new float2(0, 0), new float2(4, 0), new float2(4, 4), new float2(0, 4) }), Is.True);
            Assert.That(PolygonCheck.IsSimple(new[]
            {
                new float2(0, 0), new float2(6, 0), new float2(6, 6), new float2(3, 2), new float2(0, 6),
            }), Is.True);
        }

        [Test]
        public void IsSimple_BowTie_IsNotSimple()
        {
            Assert.That(PolygonCheck.IsSimple(new[] { new float2(0, 0), new float2(4, 4), new float2(4, 0), new float2(0, 4) }), Is.False);
        }

        [Test]
        public void IsSimple_TooFewPoints_IsNotSimple()
        {
            Assert.That(PolygonCheck.IsSimple(new[] { new float2(0, 0), new float2(1, 0) }), Is.False);
        }
    }
}
