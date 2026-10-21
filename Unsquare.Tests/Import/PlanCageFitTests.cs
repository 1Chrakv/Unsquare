using NUnit.Framework;
using Unity.Mathematics;
using Unsquare.Core.Cage;
using Unsquare.Core.Import;

namespace Unsquare.Tests.Import
{
    public class PlanCageFitTests
    {
        private static readonly CageBox Box = new(new float3(-8, 0, -12), new float3(8, 20, 12));

        [Test]
        public void TryFit_ExactBilinearTargets_RecoversCorners()
        {
            var wanted = new[] { new float2(-9, -12), new float2(8, -13), new float2(-7, 13), new float2(10, 12) };
            var from = Grid(5);
            var to = new float2[from.Length];
            for (int i = 0; i < from.Length; i++)
                to[i] = Bilinear(wanted, from[i]);

            Assert.That(PlanCageFit.TryFit(Box, from, to, out var corners, out float error), Is.True);
            for (int i = 0; i < 4; i++)
                Assert.That(math.distance(corners[i], wanted[i]), Is.LessThan(1e-4f), $"corner {i}");
            Assert.That(error, Is.LessThan(1e-4f));
        }

        [Test]
        public void TryFit_Translation_MovesEveryCornerAlike()
        {
            var from = Grid(3);
            var to = new float2[from.Length];
            for (int i = 0; i < from.Length; i++)
                to[i] = from[i] + new float2(2, -1);

            Assert.That(PlanCageFit.TryFit(Box, from, to, out var corners, out _), Is.True);
            Assert.That(math.distance(corners[0], new float2(-6, -13)), Is.LessThan(1e-4f));
            Assert.That(math.distance(corners[3], new float2(10, 11)), Is.LessThan(1e-4f));
        }

        [Test]
        public void TryFit_CollinearSamples_ReturnsFalse()
        {
            var from = new[] { new float2(-8, 0), new float2(0, 0), new float2(8, 0), new float2(4, 0) };
            Assert.That(PlanCageFit.TryFit(Box, from, from, out _, out _), Is.False);
        }

        private static float2[] Grid(int n)
        {
            var points = new float2[n * n];
            for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
                points[i * n + j] = new float2(math.lerp(-8f, 8f, i / (n - 1f)), math.lerp(-12f, 12f, j / (n - 1f)));
            return points;
        }

        private static float2 Bilinear(float2[] corners, float2 p)
        {
            float u = (p.x + 8) / 16, v = (p.y + 12) / 24;
            return math.lerp(math.lerp(corners[0], corners[1], u), math.lerp(corners[2], corners[3], u), v);
        }
    }
}
