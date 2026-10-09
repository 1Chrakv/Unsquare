using NUnit.Framework;
using Unity.Mathematics;
using Unsquare.Core.Cage;
using Unsquare.Core.Deformation;

namespace Unsquare.Tests.Deformation
{
    public class RigidDeformerTests
    {
        private static readonly CageBox Box = new(new float3(-5, 0, -5), new float3(5, 10, 5));
        private static readonly float3 Up = new(0, 1, 0);

        private static bool Near(float3 a, float3 b, float eps = 1e-4f) => math.all(math.abs(a - b) <= eps);

        [Test]
        public void Deform_IdentityCage_KeepsPose()
        {
            var pose = new RigidPose(new float3(1, 10, -2), quaternion.RotateY(0.7f));
            var result = RigidDeformer.Deform(new TrilinearCageDeformation(Box, CageOffsets.Identity), pose);
            Assert.That(Near(result.Position, pose.Position), Is.True);
            Assert.That(math.abs(math.dot(result.Rotation.value, pose.Rotation.value)), Is.EqualTo(1f).Within(1e-4f));
        }

        [Test]
        public void Deform_TranslatedCage_MovesPositionKeepsRotation()
        {
            var meters = new float3[8];
            for (int i = 0; i < meters.Length; i++)
                meters[i] = new float3(2, 1, 0);
            var deformation = new TrilinearCageDeformation(Box, CageOffsets.FromMeters(meters));
            var pose = new RigidPose(new float3(0, 10, 0), quaternion.RotateY(0.3f));

            var result = RigidDeformer.Deform(deformation, pose);

            Assert.That(Near(result.Position, new float3(2, 11, 0)), Is.True);
            Assert.That(math.abs(math.dot(result.Rotation.value, pose.Rotation.value)), Is.EqualTo(1f).Within(1e-4f));
        }

        [Test]
        public void Deform_LeaningCage_TiltsUprightPropTowardsLean()
        {
            // Top face moved +x: walls lean towards +x, so a chimney on the roof tilts that way.
            var offsets = CageOffsets.Identity.Move(CageHandle.Face(3), new float3(4, 0, 0));
            var deformation = new TrilinearCageDeformation(Box, offsets);
            var result = RigidDeformer.Deform(deformation, new RigidPose(new float3(0, 10, 0), quaternion.identity));

            float3 propUp = math.rotate(result.Rotation, Up);
            Assert.That(Near(result.Position, new float3(4, 10, 0)), Is.True);
            Assert.That(propUp.x, Is.GreaterThan(0.1f));
            Assert.That(math.length(propUp), Is.EqualTo(1f).Within(1e-4f));
        }

        [Test]
        public void DeformUpright_LeaningCage_StaysUpright()
        {
            var offsets = CageOffsets.Identity.Move(CageHandle.Face(3), new float3(4, 0, 0));
            var deformation = new TrilinearCageDeformation(Box, offsets);
            var result = RigidDeformer.DeformUpright(deformation, new RigidPose(new float3(0, 5, 0), quaternion.identity));

            Assert.That(Near(math.rotate(result.Rotation, Up), Up), Is.True);
            Assert.That(Near(result.Position, new float3(2, 5, 0)), Is.True);
        }

        [Test]
        public void DeformUpright_RotatedCage_TakesYaw()
        {
            // Rotate every corner 30° around Y: the cage turns rigidly, so ground props turn with it.
            var turn = quaternion.RotateY(math.radians(30f));
            var meters = new float3[8];
            for (int i = 0; i < meters.Length; i++)
                meters[i] = math.rotate(turn, Box.Corner(i)) - Box.Corner(i);
            var deformation = new TrilinearCageDeformation(Box, CageOffsets.FromMeters(meters));

            var result = RigidDeformer.DeformUpright(deformation, new RigidPose(new float3(0, 5, 0), quaternion.identity));

            float3 forward = math.rotate(result.Rotation, new float3(0, 0, 1));
            Assert.That(math.degrees(math.atan2(forward.x, forward.z)), Is.EqualTo(30f).Within(1f));
            Assert.That(Near(math.rotate(result.Rotation, Up), Up), Is.True);
        }

        [Test]
        public void Place_DeepInside_EqualsInteriorRotation()
        {
            var big = new CageBox(new float3(-20, 0, -20), new float3(20, 10, 20));
            var offsets = CageOffsets.Identity.Move(CageHandle.Corner(5), new float3(0, 0, -6));
            var deformation = new TrilinearCageDeformation(big, offsets);
            var pose = new RigidPose(new float3(0, 0, 0), quaternion.identity);

            var placed = RigidDeformer.Place(deformation, big, pose, PlacementMode.Upright);
            var interior = RigidDeformer.DeformUpright(deformation, pose);

            Assert.That(math.abs(math.dot(placed.Rotation.value, interior.Rotation.value)), Is.EqualTo(1f).Within(1e-5f));
        }

        [Test]
        public void Place_InFrontOfFacade_TakesFacadeRotation()
        {
            var offsets = CageOffsets.Identity.Move(CageHandle.Corner(5), new float3(0, 0, -4));
            var deformation = new TrilinearCageDeformation(Box, offsets);
            var anchor = new float3(3, 0, 6);
            var turned = quaternion.RotateY(0.4f);

            var placed = RigidDeformer.Place(deformation, Box, new RigidPose(anchor, turned), PlacementMode.Upright);
            var facade = FaceAlignment.FaceRotation(deformation, Box, anchor, 2, 1, PlacementMode.Upright);

            var expected = math.mul(facade, turned);
            Assert.That(math.abs(math.dot(placed.Rotation.value, expected.value)), Is.EqualTo(1f).Within(1e-4f));
        }

        [Test]
        public void Deform_ComposesWithExistingRotation()
        {
            var offsets = CageOffsets.Identity.Move(CageHandle.Face(3), new float3(4, 0, 0));
            var deformation = new TrilinearCageDeformation(Box, offsets);
            var turned = quaternion.RotateY(math.PI / 2);
            var position = new float3(0, 10, 0);

            var plain = RigidDeformer.Deform(deformation, new RigidPose(position, quaternion.identity));
            var composed = RigidDeformer.Deform(deformation, new RigidPose(position, turned));

            var expected = math.mul(plain.Rotation, turned);
            Assert.That(math.abs(math.dot(composed.Rotation.value, expected.value)), Is.EqualTo(1f).Within(1e-4f));
        }
    }
}
