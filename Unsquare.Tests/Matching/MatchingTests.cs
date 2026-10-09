using NUnit.Framework;
using Unity.Mathematics;
using Unsquare.Core.Deformation;
using Unsquare.Core.Matching;

namespace Unsquare.Tests.Matching
{
    public class MatchingTests
    {
        private static readonly float3[] Square =
            { new(0, 0, 0), new(4, 0, 0), new(4, 0, 4), new(0, 0, 4) };

        [Test]
        public void Polygon_SameNodesRotated_IsZero()
        {
            var rotated = new[] { Square[2], Square[3], Square[0], Square[1] };
            Assert.That(ShapeDistance.Polygon(rotated, Square), Is.EqualTo(0f).Within(1e-5f));
        }

        [Test]
        public void Polygon_IgnoresHeight()
        {
            var lifted = new[] { Square[0] + new float3(0, 3, 0), Square[1], Square[2], Square[3] };
            Assert.That(ShapeDistance.Polygon(lifted, Square), Is.EqualTo(0f).Within(1e-5f));
        }

        [Test]
        public void Polygon_DifferentCounts_IsNaN()
        {
            Assert.That(float.IsNaN(ShapeDistance.Polygon(new[] { Square[0], Square[1], Square[2] }, Square)), Is.True);
        }

        [Test]
        public void Curve_ReversedMatch_IsDetected()
        {
            var expected = new CubicCurve(new float3(0, 0, 0), new float3(1, 0, 0), new float3(2, 0, 0), new float3(3, 0, 0));
            float distance = ShapeDistance.Curve(expected.Reversed(), expected, out bool reversed);
            Assert.That(distance, Is.EqualTo(0f).Within(1e-5f));
            Assert.That(reversed, Is.True);
        }

        [Test]
        public void Match_PrefersLowestCostAndRespectsLimit()
        {
            // Instance 0 fits expected 1 best; instance 1 fits expected 1 too but less well, so it takes 0.
            float[,] costs = { { 5f, 0.1f }, { 1f, 0.5f }, { 50f, 60f } };
            int[] result = GreedyMatcher.Match(3, 2, (i, j) => costs[i, j], maxCost: 10f);

            Assert.That(result, Is.EqualTo(new[] { 1, 0, -1 }));
        }

        [Test]
        public void Match_NaNCost_NeverMatches()
        {
            int[] result = GreedyMatcher.Match(1, 1, (i, j) => float.NaN, maxCost: 10f);
            Assert.That(result, Is.EqualTo(new[] { -1 }));
        }
    }
}
