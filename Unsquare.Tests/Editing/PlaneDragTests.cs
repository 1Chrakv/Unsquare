using NUnit.Framework;
using Unity.Mathematics;
using Unsquare.Core.Editing;

namespace Unsquare.Tests.Editing
{
    public class PlaneDragTests
    {
        private static readonly float3 Camera = new(0, 10, 10);

        private static CageRay RayTo(float3 target) => new(Camera, target - Camera);

        [Test]
        public void TryDelta_MouseMoves_FollowsHitOnPlane()
        {
            Assert.That(PlaneDrag.TryStart(2f, RayTo(new float3(0, 2, 0)), out var drag), Is.True);
            Assert.That(drag.TryDelta(RayTo(new float3(3, 2, -4)), out var delta), Is.True);
            Assert.That(delta.x, Is.EqualTo(3f).Within(1e-4f));
            Assert.That(delta.z, Is.EqualTo(-4f).Within(1e-4f));
        }

        [Test]
        public void TryDelta_Always_KeepsHeight()
        {
            Assert.That(PlaneDrag.TryStart(2f, RayTo(new float3(1, 2, 1)), out var drag), Is.True);
            Assert.That(drag.TryDelta(RayTo(new float3(5, 0, -3)), out var delta), Is.True);
            Assert.That(delta.y, Is.EqualTo(0f));
        }

        [Test]
        public void TryStart_RayParallelToPlane_Fails()
        {
            var ray = new CageRay(new float3(0, 2.01f, 10), new float3(0, -0.01f, -1));
            Assert.That(PlaneDrag.TryStart(2f, ray, out _), Is.False);
        }

        [Test]
        public void TryDelta_PlaneBehindCamera_Fails()
        {
            Assert.That(PlaneDrag.TryStart(2f, RayTo(float3.zero), out var drag), Is.True);
            Assert.That(drag.TryDelta(new CageRay(Camera, new float3(0, 1, -1)), out _), Is.False);
        }
    }
}
