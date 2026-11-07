using NUnit.Framework;
using Unity.Mathematics;
using Unsquare.Core.Cage;
using Unsquare.Core.Deformation;

namespace Unsquare.Tests.Deformation
{
    public class PropAnchorTests
    {
        private static readonly CageBox Footprint = new(new float3(-6f, -2f, -5f), new float3(6f, 15f, 5f));

        // 4×6 lot: x −16..16, z −24..24.
        private static readonly CageBox Lot = new(new float3(-16f, -2f, -24f), new float3(16f, 15f, 24f));

        // Fence segment 6 m long along local x, pivot at its middle and its foot.
        private static readonly MinMaxBounds SegmentBounds = new(new float3(-3f, 0f, -0.1f), new float3(3f, 2f, 0.1f));

        // FR_PlaWall16m from the game log: pivot at the wall's top end, mesh hanging 9.8 m down into the terrain.
        private static readonly MinMaxBounds FrenchWall =
            new(new float3(-0.3f, -9.799f, -0.18f), new float3(15.701f, 0.3005f, 0.18f));

        private static readonly CageBox FrenchMeshBox =
            new(new float3(-16.07f, -0.006f, -1.72f), new float3(16.07f, 19.67f, 24.65f));

        private static RigidPose Pose(float3 position, float yawDegrees = 0f) =>
            new(position, quaternion.RotateY(math.radians(yawDegrees)));

        private static bool OnLot(float groundY, float3 anchor) =>
            PropAnchor.StandsOnLot(groundY, Footprint, Pose(anchor), MinMaxBounds.Empty);

        private static LinearFootprint Linear(MinMaxBounds bounds)
        {
            Assert.That(LinearFootprint.TryCreate(bounds, out var footprint), Is.True);
            return footprint;
        }

        private static bool AlongEdge(float3 position, float yawDegrees, MinMaxBounds bounds) =>
            PropAnchor.RunsAlongLotEdge(Lot, 0f, Pose(position, yawDegrees), bounds, Linear(bounds));

        [Test]
        public void ReachesGround_PivotOnTopMeshHangingDown_True()
        {
            Assert.That(PropAnchor.ReachesGround(0f, Pose(new float3(-15.8f, 2.5f, -15.6f), -90f), FrenchWall), Is.True);
        }

        [Test]
        public void ReachesGround_WallLight_False()
        {
            var light = new MinMaxBounds(new float3(-0.135f, -0.052f, 0f), new float3(0.135f, 0.052f, 0.088f));
            Assert.That(PropAnchor.ReachesGround(0f, Pose(new float3(0.5f, 2.23f, 1.4f), 180f), light), Is.False);
        }

        [Test]
        public void ReachesGround_EmptyBounds_UsesAnchor()
        {
            Assert.That(PropAnchor.ReachesGround(0f, Pose(new float3(0f, 0.3f, 0f)), MinMaxBounds.Empty), Is.True);
            Assert.That(PropAnchor.ReachesGround(0f, Pose(new float3(0f, 0.8f, 0f)), MinMaxBounds.Empty), Is.False);
        }

        [Test]
        public void StandsOnLot_GroundOutsideFootprint_True()
        {
            Assert.That(OnLot(0f, new float3(0f, 0.1f, 9f)), Is.True);
            Assert.That(OnLot(0f, new float3(-10f, 0f, 0f)), Is.True);
        }

        [Test]
        public void StandsOnLot_OnRoof_False()
        {
            Assert.That(OnLot(0f, new float3(8f, 15f, 0f)), Is.False);
        }

        [Test]
        public void StandsOnLot_InsideFootprint_False()
        {
            Assert.That(OnLot(0f, new float3(2f, 0f, 3f)), Is.False);
        }

        [Test]
        public void StandsOnLot_WithinMargin_False()
        {
            Assert.That(OnLot(0f, new float3(0f, 0f, 5.3f)), Is.False);
        }

        [Test]
        public void StandsOnLot_RaisedFrame_UsesGroundHeight()
        {
            var anchor = new float3(0f, -4f, 9f);
            Assert.That(OnLot(-4f, anchor), Is.True);
            Assert.That(OnLot(0f, anchor), Is.False);
        }

        [Test]
        public void StandsOnLot_FrenchWall_True()
        {
            var pose = Pose(new float3(-15.8f, 2.5f, -15.6f), -90f);
            Assert.That(PropAnchor.StandsOnLot(0f, FrenchMeshBox, pose, FrenchWall), Is.True);
        }

        [Test]
        public void RunsAlongLotEdge_FrenchWall_True()
        {
            Assert.That(AlongEdge(new float3(15.8f, 2.5f, -15.6f), -90f, FrenchWall), Is.True, "right side");
            Assert.That(AlongEdge(new float3(-0.035f, 2.5f, -23.7f), 180f, FrenchWall), Is.True, "back side");
        }

        [Test]
        public void RunsAlongLotEdge_FenceOnSide_True()
        {
            Assert.That(AlongEdge(new float3(0f, 0f, -23.5f), 0f, SegmentBounds), Is.True, "back side, inside");
            Assert.That(AlongEdge(new float3(5f, 0.2f, 25f), 0f, SegmentBounds), Is.True, "front side, outside");
            Assert.That(AlongEdge(new float3(-15f, 0f, 10f), 90f, SegmentBounds), Is.True, "left side, yawed");
            Assert.That(AlongEdge(new float3(16f, 0f, -20f), 100f, SegmentBounds), Is.True, "right side, 10° off");
        }

        [Test]
        public void RunsAlongLotEdge_Perpendicular_False()
        {
            Assert.That(AlongEdge(new float3(0f, 0f, -23.5f), 90f, SegmentBounds), Is.False);
            Assert.That(AlongEdge(new float3(0f, 0f, -23.5f), 30f, SegmentBounds), Is.False);
        }

        [Test]
        public void RunsAlongLotEdge_FarFromEdge_False()
        {
            Assert.That(AlongEdge(new float3(0f, 0f, -10f), 0f, SegmentBounds), Is.False);
            Assert.That(AlongEdge(new float3(30f, 0f, -24f), 0f, SegmentBounds), Is.False, "beyond the side's end");
        }

        [Test]
        public void RunsAlongLotEdge_Raised_False()
        {
            Assert.That(AlongEdge(new float3(0f, 12f, -23.5f), 0f, SegmentBounds), Is.False);
        }

        [Test]
        public void RunsAlongLotEdge_PivotAtEnd_UsesCentre()
        {
            // Pivot beyond the back side's end (x = −20), but the segment's centre (x = −16) is on the side.
            var endPivot = new MinMaxBounds(new float3(0f, 0f, -0.1f), new float3(8f, 2f, 0.1f));
            Assert.That(AlongEdge(new float3(-20f, 0f, -23f), 0f, endPivot), Is.True);
            Assert.That(AlongEdge(new float3(-20f, 0f, -23f), 0f, SegmentBounds), Is.False);
        }
    }
}
