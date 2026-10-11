using NUnit.Framework;
using Unity.Mathematics;
using Unsquare.Core.Cage;
using Unsquare.Core.Editing;

namespace Unsquare.Tests.Editing
{
    public class CageProximityTests
    {
        // Largest size 20 m → margin max(5, 0.25 × 20) = 5 m.
        private static readonly CageBox Box = new(float3.zero, new float3(10, 8, 20));
        private static readonly float3 Down = new(0, -1, 0);

        private static bool IsNear(float3 origin, float3 direction, CageOffsets offsets = null, CageBox? box = null) =>
            CageProximity.IsNear(box ?? Box, offsets ?? CageOffsets.Identity, new CageRay(origin, direction), 0.25f, 5f);

        [Test]
        public void IsNear_RayThroughBox_IsTrue()
        {
            Assert.That(IsNear(new float3(5, 100, 10), Down), Is.True);
        }

        [Test]
        public void IsNear_RayWithinMargin_IsTrue()
        {
            Assert.That(IsNear(new float3(13, 100, 10), Down), Is.True);
        }

        [Test]
        public void IsNear_RayFarAway_IsFalse()
        {
            Assert.That(IsNear(new float3(30, 100, 10), Down), Is.False);
        }

        [Test]
        public void IsNear_SmallBox_UsesMinimumMargin()
        {
            var small = new CageBox(float3.zero, new float3(2, 2, 2));
            Assert.That(IsNear(new float3(6, 100, 1), Down, box: small), Is.True);
            Assert.That(IsNear(new float3(8, 100, 1), Down, box: small), Is.False);
        }

        [Test]
        public void IsNear_BoxBehindCamera_IsFalse()
        {
            Assert.That(IsNear(new float3(5, 100, 10), -Down), Is.False);
        }

        [Test]
        public void IsNear_RaisedCage_CountsNewExtent()
        {
            // Horizontal ray 20 m above the rest box: far for the rest box, near once the top is raised by 16 m.
            var raised = CageOffsets.Identity.Move(CageHandle.Face(3), new float3(0, 16, 0));
            var origin = new float3(-100, 28, 10);
            var direction = new float3(1, 0, 0);
            Assert.That(IsNear(origin, direction), Is.False);
            Assert.That(IsNear(origin, direction, raised), Is.True);
        }
    }
}
