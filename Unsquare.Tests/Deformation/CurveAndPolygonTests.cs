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

        // Square with a square hole, joined to the outline by a zero-width bridge along y = 5.
        private static readonly float2[] SquareWithHole =
        {
            new(0, 5), new(0, 0), new(10, 0), new(10, 10), new(0, 10), new(0, 5),
            new(3, 5), new(3, 7), new(7, 7), new(7, 3), new(3, 3), new(3, 5),
        };

        private static float2[] Map(float2[] points, System.Func<float2, float2> f)
        {
            var result = new float2[points.Length];
            for (int i = 0; i < points.Length; i++)
                result[i] = f(points[i]);
            return result;
        }

        [Test]
        public void CountContacts_BridgedHole_AlreadyTouches()
        {
            Assert.That(PolygonCheck.CountContacts(SquareWithHole), Is.GreaterThan(0));
            Assert.That(PolygonCheck.IsSimple(SquareWithHole), Is.False);
        }

        [Test]
        public void AddsContacts_BridgedHoleTranslatedOrTapered_IsAccepted()
        {
            var moved = Map(SquareWithHole, p => p + new float2(4, -2));
            var tapered = Map(SquareWithHole, p => new float2(p.x * (1f + p.y * 0.03f), p.y));
            Assert.That(PolygonCheck.AddsContacts(SquareWithHole, moved), Is.False);
            Assert.That(PolygonCheck.AddsContacts(SquareWithHole, tapered), Is.False);
        }

        [Test]
        public void CountContacts_DuplicatePoint_CountsLikeWithout()
        {
            var square = new[] { new float2(0, 0), new float2(10, 0), new float2(10, 10), new float2(0, 10) };
            var duplicated = new[] { new float2(0, 0), new float2(10, 0), new float2(10, 0), new float2(10, 10), new float2(0, 10) };
            Assert.That(PolygonCheck.CountContacts(duplicated), Is.EqualTo(PolygonCheck.CountContacts(square)));
            Assert.That(PolygonCheck.IsSimple(duplicated), Is.True);
        }

        [Test]
        public void AddsContacts_CornerPulledAcrossOppositeEdge_IsRejected()
        {
            var square = new[] { new float2(0, 0), new float2(10, 0), new float2(10, 10), new float2(0, 10) };
            var folded = new[] { new float2(0, 0), new float2(10, 0), new float2(-5, 5), new float2(0, 10) };
            Assert.That(PolygonCheck.AddsContacts(square, folded), Is.True);
        }

        [Test]
        public void IsSimple_TooFewPoints_IsNotSimple()
        {
            Assert.That(PolygonCheck.IsSimple(new[] { new float2(0, 0), new float2(1, 0) }), Is.False);
        }
    }
}
