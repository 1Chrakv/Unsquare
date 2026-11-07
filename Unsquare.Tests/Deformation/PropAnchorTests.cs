using NUnit.Framework;
using Unity.Mathematics;
using Unsquare.Core.Cage;
using Unsquare.Core.Deformation;

namespace Unsquare.Tests.Deformation
{
    public class PropAnchorTests
    {
        private static readonly CageBox Footprint = new(new float3(-6f, -2f, -5f), new float3(6f, 15f, 5f));

        [Test]
        public void StandsOnLot_GroundOutsideFootprint_True()
        {
            Assert.That(PropAnchor.StandsOnLot(0f, Footprint, new float3(0f, 0.1f, 9f)), Is.True);
            Assert.That(PropAnchor.StandsOnLot(0f, Footprint, new float3(-10f, 0f, 0f)), Is.True);
        }

        [Test]
        public void StandsOnLot_OnRoof_False()
        {
            Assert.That(PropAnchor.StandsOnLot(0f, Footprint, new float3(8f, 15f, 0f)), Is.False);
        }

        [Test]
        public void StandsOnLot_InsideFootprint_False()
        {
            Assert.That(PropAnchor.StandsOnLot(0f, Footprint, new float3(2f, 0f, 3f)), Is.False);
        }

        [Test]
        public void StandsOnLot_WithinMargin_False()
        {
            Assert.That(PropAnchor.StandsOnLot(0f, Footprint, new float3(0f, 0f, 5.3f)), Is.False);
        }

        [Test]
        public void StandsOnLot_RaisedFrame_UsesGroundHeight()
        {
            var anchor = new float3(0f, -4f, 9f);
            Assert.That(PropAnchor.StandsOnLot(-4f, Footprint, anchor), Is.True);
            Assert.That(PropAnchor.StandsOnLot(0f, Footprint, anchor), Is.False);
        }
    }
}
