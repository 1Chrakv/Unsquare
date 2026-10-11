using NUnit.Framework;
using Unity.Mathematics;
using Unsquare.Core.Editing;

namespace Unsquare.Tests.Editing
{
    public class AxisDragTests
    {
        private static readonly float3 Camera = new(0, 10, 10);

        private static CageRay RayTo(float3 target) => new(Camera, target - Camera);

        [Test]
        public void TryDelta_MouseOverAxisPoint_FollowsIt()
        {
            Assert.That(AxisDrag.TryStart(float3.zero, 0, RayTo(float3.zero), out var drag), Is.True);
            Assert.That(drag.TryDelta(RayTo(new float3(3, 0, 0)), out var delta), Is.True);
            Assert.That(delta.x, Is.EqualTo(3f).Within(1e-4f));
            Assert.That(delta.y, Is.EqualTo(0f));
            Assert.That(delta.z, Is.EqualTo(0f));
        }

        [Test]
        public void TryDelta_GrabOffAxisOrigin_KeepsGrabbedSpot()
        {
            // Grabbed 1 m along the arrow; moving the mouse to 4 m moves the handle by 3 m.
            Assert.That(AxisDrag.TryStart(float3.zero, 1, RayTo(new float3(0, 1, 0)), out var drag), Is.True);
            Assert.That(drag.TryDelta(RayTo(new float3(0, 4, 0)), out var delta), Is.True);
            Assert.That(delta.y, Is.EqualTo(3f).Within(1e-4f));
        }

        [Test]
        public void TryStart_RayAlongAxis_Fails()
        {
            var ray = new CageRay(new float3(-10, 0, 0), new float3(1, 0, 0.01f));
            Assert.That(AxisDrag.TryStart(float3.zero, 0, ray, out _), Is.False);
        }

        [Test]
        public void TryDelta_AxisBehindCamera_Fails()
        {
            Assert.That(AxisDrag.TryStart(float3.zero, 0, RayTo(float3.zero), out var drag), Is.True);
            Assert.That(drag.TryDelta(new CageRay(Camera, new float3(0, 1, 1)), out _), Is.False);
        }
    }
}
