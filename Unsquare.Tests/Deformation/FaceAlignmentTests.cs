using NUnit.Framework;
using Unity.Mathematics;
using Unsquare.Core.Cage;
using Unsquare.Core.Deformation;

namespace Unsquare.Tests.Deformation
{
    public class FaceAlignmentTests
    {
        // 20 m wide (x), 10 m high, 10 m deep; +z is the street-side facade.
        private static readonly CageBox Box = new(new float3(-10, 0, -5), new float3(10, 10, 5));
        private static readonly float3 Up = new(0, 1, 0);

        private static float YawDegrees(quaternion q) => math.degrees(FaceAlignment.YawOf(q));

        [Test]
        public void TryFindFace_OutsideFront_IsFrontWithFullWeight()
        {
            Assert.That(FaceAlignment.TryFindFace(Box, new float3(3, 0, 6), PlacementMode.Upright,
                out int axis, out int side, out float weight), Is.True);
            Assert.That((axis, side), Is.EqualTo((2, 1)));
            Assert.That(weight, Is.EqualTo(1f));
        }

        [Test]
        public void TryFindFace_WeightFadesWithDepth()
        {
            var big = new CageBox(new float3(-20, 0, -20), new float3(20, 10, 20));
            FaceAlignment.TryFindFace(big, new float3(0, 0, 20), PlacementMode.Upright, out _, out _, out float onFace);
            FaceAlignment.TryFindFace(big, new float3(0, 0, 17), PlacementMode.Upright, out _, out _, out float halfway);
            bool found = FaceAlignment.TryFindFace(big, new float3(0, 0, 13), PlacementMode.Upright, out _, out _, out float deep);

            Assert.That(onFace, Is.EqualTo(1f));
            Assert.That(halfway, Is.GreaterThan(0f).And.LessThan(1f));
            Assert.That(found, Is.False);
            Assert.That(deep, Is.EqualTo(0f));
        }

        [Test]
        public void TryFindFace_UprightIgnoresRoof_FollowPicksIt()
        {
            var roofProp = new float3(0, 10.2f, 0);
            FaceAlignment.TryFindFace(Box, roofProp, PlacementMode.Upright, out int uprightAxis, out _, out _);
            FaceAlignment.TryFindFace(Box, roofProp, PlacementMode.Follow, out int followAxis, out int side, out _);

            Assert.That(uprightAxis, Is.Not.EqualTo(1));
            Assert.That((followAxis, side), Is.EqualTo((1, 1)));
        }

        [Test]
        public void FaceRotation_FrontCornerPulledIn_MatchesFacadeYaw()
        {
            // Bottom front-right corner pulled 4 m back: the front facade's bottom edge turns by atan(4/20).
            var offsets = CageOffsets.Identity.Move(CageHandle.Corner(5), new float3(0, 0, -4));
            var deformation = new TrilinearCageDeformation(Box, offsets);

            var rotation = FaceAlignment.FaceRotation(deformation, Box, new float3(3, 0, 6), 2, 1, PlacementMode.Upright);

            Assert.That(YawDegrees(rotation), Is.EqualTo(math.degrees(math.atan2(4f, 20f))).Within(0.5f));
        }

        [Test]
        public void FaceRotation_SideFacadeSkewed_MatchesSideYaw()
        {
            // Bottom front-right corner pushed 4 m out in x: the +x facade's bottom edge turns by atan(4/10).
            var offsets = CageOffsets.Identity.Move(CageHandle.Corner(5), new float3(4, 0, 0));
            var deformation = new TrilinearCageDeformation(Box, offsets);

            var rotation = FaceAlignment.FaceRotation(deformation, Box, new float3(11, 0, 0), 0, 1, PlacementMode.Upright);

            Assert.That(YawDegrees(rotation), Is.EqualTo(math.degrees(math.atan2(4f, 10f))).Within(0.5f));
        }

        [Test]
        public void FaceRotation_RoofOfLeaningBuilding_StaysUpright()
        {
            // Lean (top face shifted): the roof stays flat, so roof props should not tilt.
            var offsets = CageOffsets.Identity.Move(CageHandle.Face(3), new float3(4, 0, 0));
            var deformation = new TrilinearCageDeformation(Box, offsets);

            var rotation = FaceAlignment.FaceRotation(deformation, Box, new float3(0, 10.2f, 0), 1, 1, PlacementMode.Follow);

            Assert.That(math.all(math.abs(math.rotate(rotation, Up) - Up) < 1e-4f), Is.True);
        }

        [TestCase(PlacementMode.Upright)]
        [TestCase(PlacementMode.Follow)]
        public void FaceRotation_RigidlyRotatedCage_EqualsThatRotation(PlacementMode mode)
        {
            var turn = quaternion.RotateY(math.radians(25f));
            var meters = new float3[8];
            for (int i = 0; i < meters.Length; i++)
                meters[i] = math.rotate(turn, Box.Corner(i)) - Box.Corner(i);
            var deformation = new TrilinearCageDeformation(Box, CageOffsets.FromMeters(meters));

            var rotation = FaceAlignment.FaceRotation(deformation, Box, new float3(0, 0, 6), 2, 1, mode);

            Assert.That(math.abs(math.dot(rotation.value, turn.value)), Is.EqualTo(1f).Within(1e-3f));
        }
    }
}
