using NUnit.Framework;
using Unity.Mathematics;
using Unsquare.Core.Cage;
using Unsquare.Core.Deformation;

namespace Unsquare.Tests.Deformation
{
    public class TrilinearCageDeformationTests
    {
        private static readonly CageBox Box = new(new float3(-6, 0, -8), new float3(6, 15, 8));

        private static CageOffsets Skewed() => CageOffsets.Identity
            .Move(CageHandle.Corner(7), new float3(2f, 3f, -1f))
            .Move(CageHandle.Face(3), new float3(1.5f, 0f, 0.5f))
            .Move(CageHandle.Edge(9), new float3(0f, -0.8f, 0.4f));

        [Test]
        public void Identity_KeepsPointsAndHasUnitJacobian()
        {
            var d = new TrilinearCageDeformation(Box, CageOffsets.Identity);
            var p = new float3(1.2f, 3.4f, -5.6f);
            Assert.That(Near(d.Apply(p, out var j), p), Is.True);
            Assert.That(Near(j, float3x3.identity), Is.True);
        }

        [Test]
        public void Corners_MapToMovedCorners()
        {
            var offsets = Skewed();
            var d = new TrilinearCageDeformation(Box, offsets);
            for (int i = 0; i < CageTopology.CornerCount; i++)
                Assert.That(Near(d.Apply(Box.Corner(i), out _), Box.Corner(i) + offsets.Meters(i)), Is.True, $"corner {i}");
        }

        [Test]
        public void SameOffsetEverywhere_IsTranslation()
        {
            var meters = new float3[8];
            for (int i = 0; i < 8; i++)
                meters[i] = new float3(1, 2, 3);
            var d = new TrilinearCageDeformation(Box, CageOffsets.FromMeters(meters));
            var p = new float3(20, -4, 3); // outside the box: extrapolated
            Assert.That(Near(d.Apply(p, out var j), p + new float3(1, 2, 3)), Is.True);
            Assert.That(Near(j, float3x3.identity), Is.True);
        }

        [Test]
        public void MovingTopFaceUp_ScalesHeightLinearly()
        {
            var offsets = CageOffsets.Identity.Move(CageHandle.Face(3), new float3(0, 15, 0));
            var d = new TrilinearCageDeformation(Box, offsets);
            Assert.That(Near(d.Apply(new float3(0, 7.5f, 0), out var j), new float3(0, 15, 0)), Is.True);
            Assert.That(j.c1.y, Is.EqualTo(2f).Within(1e-4f));
        }

        [Test]
        public void Jacobian_MatchesFiniteDifferences()
        {
            var d = new TrilinearCageDeformation(Box, Skewed());
            var p = new float3(2.5f, 9f, -3f);
            d.Apply(p, out var j);
            const float h = 1e-2f;
            for (int axis = 0; axis < 3; axis++)
            {
                var e = float3.zero;
                e[axis] = h;
                float3 numeric = (d.Apply(p + e, out _) - d.Apply(p - e, out _)) / (2 * h);
                Assert.That(Near(numeric, j[axis], 1e-3f), Is.True, $"axis {axis}: {numeric} vs {j[axis]}");
            }
        }

        internal static bool Near(float3 a, float3 b, float eps = 1e-4f) => math.all(math.abs(a - b) <= eps);

        private static bool Near(float3x3 a, float3x3 b) => Near(a.c0, b.c0) && Near(a.c1, b.c1) && Near(a.c2, b.c2);
    }
}
