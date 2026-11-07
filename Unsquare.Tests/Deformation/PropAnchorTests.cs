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

        // 4×6 lot: x −16..16, z −24..24. Fence segment 6 m long along local x, pivot at its middle.
        private static readonly CageBox Lot = new(new float3(-16f, -2f, -24f), new float3(16f, 15f, 24f));

        private static LinearFootprint Fence(float3 min, float3 max)
        {
            Assert.That(LinearFootprint.TryCreate(new MinMaxBounds(min, max), out var footprint), Is.True);
            return footprint;
        }

        private static readonly LinearFootprint Segment = Fence(new float3(-3f, 0f, -0.1f), new float3(3f, 2f, 0.1f));

        private static bool AlongEdge(float3 position, float yaw, LinearFootprint footprint) =>
            PropAnchor.RunsAlongLotEdge(Lot, 0f, new RigidPose(position, quaternion.RotateY(yaw)), footprint);

        [Test]
        public void RunsAlongLotEdge_FenceOnSide_True()
        {
            Assert.That(AlongEdge(new float3(0f, 0f, -23.5f), 0f, Segment), Is.True, "back side, inside");
            Assert.That(AlongEdge(new float3(5f, 0.2f, 25f), 0f, Segment), Is.True, "front side, outside");
            Assert.That(AlongEdge(new float3(-15f, 0f, 10f), math.PI / 2, Segment), Is.True, "left side, yawed");
            Assert.That(AlongEdge(new float3(16f, 0f, -20f), math.radians(100f), Segment), Is.True, "right side, 10° off");
        }

        [Test]
        public void RunsAlongLotEdge_Perpendicular_False()
        {
            Assert.That(AlongEdge(new float3(0f, 0f, -23.5f), math.PI / 2, Segment), Is.False);
            Assert.That(AlongEdge(new float3(0f, 0f, -23.5f), math.radians(30f), Segment), Is.False);
        }

        [Test]
        public void RunsAlongLotEdge_FarFromEdge_False()
        {
            Assert.That(AlongEdge(new float3(0f, 0f, -10f), 0f, Segment), Is.False);
            Assert.That(AlongEdge(new float3(30f, 0f, -24f), 0f, Segment), Is.False, "beyond the side's end");
        }

        [Test]
        public void RunsAlongLotEdge_Raised_False()
        {
            Assert.That(AlongEdge(new float3(0f, 12f, -23.5f), 0f, Segment), Is.False);
        }

        [Test]
        public void RunsAlongLotEdge_PivotAtEnd_UsesCentre()
        {
            // Pivot beyond the back side's end (x = −20), but the segment's centre (x = −16) is on the side.
            var endPivot = Fence(new float3(0f, 0f, -0.1f), new float3(8f, 2f, 0.1f));
            Assert.That(AlongEdge(new float3(-20f, 0f, -23f), 0f, endPivot), Is.True);
            Assert.That(AlongEdge(new float3(-20f, 0f, -23f), 0f, Segment), Is.False);
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
