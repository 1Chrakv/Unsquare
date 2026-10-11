using NUnit.Framework;
using Unity.Mathematics;
using Unsquare.Core.Cage;
using Unsquare.Core.Editing;

namespace Unsquare.Tests.Editing
{
    public class CageProximityTests
    {
        // Largest size 20 m → margin 0.05 × 20 = 1 m.
        private const float Margin = 0.05f;
        private static readonly CageBox Box = new(float3.zero, new float3(10, 8, 20));
        private static readonly float3 Down = new(0, -1, 0);

        private static bool IsNear(float3 origin, float3 direction, CageOffsets offsets = null,
            float margin = Margin) =>
            CageProximity.IsNear(Box, offsets ?? CageOffsets.Identity, new CageRay(origin, direction), margin);

        [Test]
        public void IsNear_RayThroughBox_IsTrue()
        {
            Assert.That(IsNear(new float3(5, 100, 10), Down), Is.True);
        }

        [Test]
        public void IsNear_RayWithinMargin_IsTrue()
        {
            Assert.That(IsNear(new float3(10.8f, 100, 10), Down), Is.True);
        }

        [Test]
        public void IsNear_RayFarAway_IsFalse()
        {
            Assert.That(IsNear(new float3(13, 100, 10), Down), Is.False);
        }

        [Test]
        public void IsNear_ZeroMargin_OnlyTheBoxCounts()
        {
            Assert.That(IsNear(new float3(9.9f, 100, 10), Down, margin: 0f), Is.True);
            Assert.That(IsNear(new float3(10.2f, 100, 10), Down, margin: 0f), Is.False);
        }

        [Test]
        public void IsNear_BoxBehindCamera_IsFalse()
        {
            Assert.That(IsNear(new float3(5, 100, 10), -Down), Is.False);
        }

        [Test]
        public void IsNear_RaisedCage_CountsNewExtent()
        {
            // Horizontal ray 12 m up: above the 8 m rest box, inside the cage once the top is raised by 6 m.
            var raised = CageOffsets.Identity.Move(CageHandle.Face(3), new float3(0, 6, 0));
            var origin = new float3(-100, 12, 10);
            var direction = new float3(1, 0, 0);
            Assert.That(IsNear(origin, direction), Is.False);
            Assert.That(IsNear(origin, direction, raised), Is.True);
        }
    }
}
