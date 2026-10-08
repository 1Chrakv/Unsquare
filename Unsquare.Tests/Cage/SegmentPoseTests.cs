using NUnit.Framework;
using Unity.Mathematics;
using Unsquare.Core.Cage;

namespace Unsquare.Tests.Cage
{
    public class SegmentPoseTests
    {
        private static readonly float3 Up = new(0, 1, 0);

        private static bool Near(float3 a, float3 b, float eps = 1e-4f) => math.all(math.abs(a - b) <= eps);

        [Test]
        public void TryCreate_CenterIsMidpointAndLengthIsDistance()
        {
            Assert.That(SegmentPose.TryCreate(new float3(1, 2, 3), new float3(5, 2, 0), out var pose), Is.True);
            Assert.That(Near(pose.Center, new float3(3, 2, 1.5f)), Is.True);
            Assert.That(pose.Length, Is.EqualTo(5f).Within(1e-4f));
        }

        [TestCase(0f, 7f, 0f, TestName = "TryCreate_VerticalUp_RotatesUpOntoDirection")]
        [TestCase(0f, -7f, 0f, TestName = "TryCreate_VerticalDown_RotatesUpOntoDirection")]
        [TestCase(4f, 0f, 0f, TestName = "TryCreate_AlongX_RotatesUpOntoDirection")]
        [TestCase(1f, 0.05f, -2f, TestName = "TryCreate_Oblique_RotatesUpOntoDirection")]
        public void TryCreate_RotatesUpOntoDirection(float x, float y, float z)
        {
            var a = new float3(-2, 1, 3);
            var b = a + new float3(x, y, z);
            Assert.That(SegmentPose.TryCreate(a, b, out var pose), Is.True);
            Assert.That(Near(math.rotate(pose.Rotation, Up), math.normalize(b - a)), Is.True);
        }

        [Test]
        public void TryCreate_UnitCylinderEndsReproduceSegmentEnds()
        {
            var a = new float3(0, 0, 0);
            var b = new float3(0.3f, 9f, -0.2f);
            SegmentPose.TryCreate(a, b, out var pose);
            float3 half = math.rotate(pose.Rotation, Up) * (pose.Length * 0.5f);
            Assert.That(Near(pose.Center - half, a), Is.True);
            Assert.That(Near(pose.Center + half, b), Is.True);
        }

        [Test]
        public void TryCreate_ZeroLength_ReturnsFalse()
        {
            Assert.That(SegmentPose.TryCreate(new float3(1), new float3(1), out _), Is.False);
        }
    }
}
