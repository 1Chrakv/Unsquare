using NUnit.Framework;
using Unity.Mathematics;
using Unsquare.Core.Cage;
using Unsquare.Core.Editing;

namespace Unsquare.Tests.Editing
{
    public class CageAnchorTests
    {
        private static readonly CageBox Box = new(new float3(-5, 0, -10), new float3(5, 8, 10));

        [Test]
        public void Top_UndeformedCage_IsCenterOfRoof()
        {
            var top = CageAnchor.Top(Box, CageOffsets.Identity);
            Assert.That(top.x, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(top.y, Is.EqualTo(8f).Within(1e-4f));
            Assert.That(top.z, Is.EqualTo(0f).Within(1e-4f));
        }

        [Test]
        public void Top_RaisedCorner_UsesHighestTopCorner()
        {
            var offsets = CageOffsets.Identity.Move(CageHandle.Corner(3), new float3(0, 4, 0));
            Assert.That(CageAnchor.Top(Box, offsets).y, Is.EqualTo(12f).Within(1e-4f));
        }

        [Test]
        public void Top_ShiftedRoof_FollowsInXz()
        {
            var offsets = CageOffsets.Identity.Move(CageHandle.Face(3), new float3(2, 0, -3));
            var top = CageAnchor.Top(Box, offsets);
            Assert.That(top.x, Is.EqualTo(2f).Within(1e-4f));
            Assert.That(top.z, Is.EqualTo(-3f).Within(1e-4f));
        }

        [Test]
        public void Top_LoweredBottom_IsIgnored()
        {
            var offsets = CageOffsets.Identity.Move(CageHandle.Face(2), new float3(0, 2, 0));
            Assert.That(CageAnchor.Top(Box, offsets).y, Is.EqualTo(8f).Within(1e-4f));
        }
    }
}
