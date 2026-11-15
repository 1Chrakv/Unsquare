using NUnit.Framework;
using Unity.Mathematics;
using Unsquare.Core.Deformation;

namespace Unsquare.Tests.Deformation
{
    public class LinearFootprintTests
    {
        private static bool TryCreate(float3 min, float3 max, out LinearFootprint footprint) =>
            LinearFootprint.TryCreate(new MinMaxBounds(min, max), out footprint);

        [Test]
        public void TryCreate_FenceAlongX_LinearWithCentreAtPivotHeight()
        {
            Assert.That(TryCreate(new float3(0, 0, -0.1f), new float3(8, 2, 0.1f), out var fence), Is.True);
            Assert.That(fence.Axis, Is.EqualTo(new float3(1, 0, 0)));
            Assert.That(fence.Centre, Is.EqualTo(new float3(4, 0, 0)));
            Assert.That(fence.Length, Is.EqualTo(8f).Within(1e-5f));
        }

        [Test]
        public void TryCreate_HedgeAlongZ_LinearAlongZ()
        {
            Assert.That(TryCreate(new float3(-0.5f, 0, -2), new float3(0.5f, 1.5f, 2), out var hedge), Is.True);
            Assert.That(hedge.Axis, Is.EqualTo(new float3(0, 0, 1)));
        }

        [Test]
        public void TryCreate_Bench_NotLinear()
        {
            Assert.That(TryCreate(new float3(-1, 0, -0.3f), new float3(1, 1, 0.3f), out _), Is.False);
        }

        [Test]
        public void TryCreate_TreeOrShed_NotLinear()
        {
            Assert.That(TryCreate(new float3(-2, 0, -2), new float3(2, 6, 2), out _), Is.False);
            Assert.That(TryCreate(new float3(-3, 0, -1.5f), new float3(3, 3, 1.5f), out _), Is.False);
        }

        [Test]
        public void TryCreate_Empty_NotLinear()
        {
            Assert.That(LinearFootprint.TryCreate(MinMaxBounds.Empty, out _), Is.False);
        }
    }
}
