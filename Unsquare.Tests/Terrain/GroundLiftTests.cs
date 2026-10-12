using NUnit.Framework;
using Unity.Mathematics;
using Unsquare.Core.Cage;
using Unsquare.Core.Deformation;
using Unsquare.Core.Terrain;

namespace Unsquare.Tests.Terrain
{
    public class GroundLiftTests
    {
        private static readonly CageBox Lot = new(new float3(-8, 0, -12), new float3(8, 15, 12));

        [Test]
        public void FromCage_NoOffsets_IsZero()
        {
            var lift = GroundLift.FromCage(Lot, CageOffsets.Identity);
            Assert.That(lift.IsZero, Is.True);
            Assert.That(lift, Is.EqualTo(LotEdgeLift.Zero));
        }

        [Test]
        public void FromCage_OneGroundCornerRaised_LiftsOnlyAdjacentEdgesLinearly()
        {
            // Corner 0 is (-x, bottom, -z): the game's corner b, end of the front and start of the right edge.
            var offsets = CageOffsets.Identity.Move(CageHandle.Corner(0), new float3(0f, 3f, 0f));
            var lift = GroundLift.FromCage(Lot, offsets);
            Assert.That(Near(lift.Front, new float3(0f, 1f, 2f)), Is.True, $"front {lift.Front}");
            Assert.That(Near(lift.Right, new float3(3f, 2f, 1f)), Is.True, $"right {lift.Right}");
            Assert.That(Near(lift.Back, float3.zero), Is.True, $"back {lift.Back}");
            Assert.That(Near(lift.Left, float3.zero), Is.True, $"left {lift.Left}");
        }

        [Test]
        public void FromCage_TopCornerWithBasement_LiftsGroundProportionally()
        {
            var basement = new CageBox(new float3(-8, -5, -12), new float3(8, 15, 12));
            // Corner 2 is (-x, top, -z); the ground is a quarter of the way up from the basement floor.
            var offsets = CageOffsets.Identity.Move(CageHandle.Corner(2), new float3(0f, 4f, 0f));
            var lift = GroundLift.FromCage(basement, offsets);
            Assert.That(lift.Right.x, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(lift.Front.x, Is.EqualTo(0f).Within(1e-4f));
        }

        [Test]
        public void FromCage_HorizontalMoveOnly_IsZero()
        {
            var offsets = CageOffsets.Identity.Move(CageHandle.Corner(5), new float3(2f, 0f, -1.5f));
            Assert.That(GroundLift.FromCage(Lot, offsets).IsZero, Is.True);
        }

        [Test]
        public void FromCage_SkewedCage_EdgeCurvesMatchDeformedGround()
        {
            var offsets = CageOffsets.Identity
                .Move(CageHandle.Corner(1), new float3(0.5f, 2.5f, 0f))
                .Move(CageHandle.Corner(4), new float3(0f, -1.2f, 0.3f))
                .Move(CageHandle.Corner(7), new float3(1f, 4f, -1f));
            var lift = GroundLift.FromCage(Lot, offsets);
            var deformation = new TrilinearCageDeformation(Lot, offsets);

            float3 a = new(8, 0, -12), b = new(-8, 0, -12), c = new(-8, 0, 12), d = new(8, 0, 12);
            AssertEdge(deformation, lift.Front, lift.Right.x, a, b);
            AssertEdge(deformation, lift.Right, lift.Back.x, b, c);
            AssertEdge(deformation, lift.Back, lift.Left.x, c, d);
            AssertEdge(deformation, lift.Left, lift.Front.x, d, a);
        }

        [Test]
        public void IsZero_BelowOneCentimetre_IsTrue()
        {
            Assert.That(new LotEdgeLift(new float3(0.005f), new float3(-0.009f), float3.zero, float3.zero).IsZero, Is.True);
            Assert.That(new LotEdgeLift(float3.zero, float3.zero, new float3(0f, -0.02f, 0f), float3.zero).IsZero, Is.False);
        }

        private static void AssertEdge(TrilinearCageDeformation deformation, float3 edge, float end, float3 from, float3 to)
        {
            for (float t = 0f; t <= 1f; t += 0.125f)
            {
                float3 p = math.lerp(from, to, t);
                float expected = deformation.Apply(p, out _).y - p.y;
                Assert.That(Bezier(edge.x, edge.y, edge.z, end, t), Is.EqualTo(expected).Within(1e-4f), $"t = {t}");
            }
        }

        private static float Bezier(float p0, float p1, float p2, float p3, float t)
        {
            float u = 1f - t;
            return u * u * u * p0 + 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t * p3;
        }

        private static bool Near(float3 a, float3 b) => math.all(math.abs(a - b) < 1e-4f);
    }
}
