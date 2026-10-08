using NUnit.Framework;
using Unity.Mathematics;
using Unsquare.Core.Deformation;

namespace Unsquare.Tests.Deformation
{
    public class JacobianRotationTests
    {
        private static void AssertSameRotation(quaternion expected, quaternion actual)
        {
            // q and -q are the same rotation.
            float dot = math.abs(math.dot(expected.value, actual.value));
            Assert.That(dot, Is.EqualTo(1f).Within(1e-4f), $"{expected} vs {actual}");
        }

        [Test]
        public void Extract_Identity_IsIdentity()
        {
            AssertSameRotation(quaternion.identity, JacobianRotation.Extract(float3x3.identity));
        }

        [Test]
        public void Extract_PureRotation_ReturnsIt()
        {
            var rotation = quaternion.EulerXYZ(0.3f, -1.1f, 0.4f);
            AssertSameRotation(rotation, JacobianRotation.Extract(new float3x3(rotation)));
        }

        [Test]
        public void Extract_RotationTimesScale_ReturnsRotation()
        {
            var rotation = quaternion.RotateY(0.8f);
            float3x3 scaled = math.mul(new float3x3(rotation), float3x3.Scale(2f, 0.5f, 3f));
            AssertSameRotation(rotation, JacobianRotation.Extract(scaled));
        }

        [Test]
        public void Extract_Shear_IsOrthonormalWithPositiveDeterminant()
        {
            // Lean: points move in +x proportional to their height.
            var shear = new float3x3(new float3(1, 0, 0), new float3(0.6f, 1, 0), new float3(0, 0, 1));
            var r = new float3x3(JacobianRotation.Extract(shear));
            float3x3 product = math.mul(math.transpose(r), r);
            Assert.That(math.all(math.abs(product.c0 - new float3(1, 0, 0)) < 1e-4f), Is.True);
            Assert.That(math.all(math.abs(product.c1 - new float3(0, 1, 0)) < 1e-4f), Is.True);
            Assert.That(math.all(math.abs(product.c2 - new float3(0, 0, 1)) < 1e-4f), Is.True);
            Assert.That(math.determinant(r), Is.EqualTo(1f).Within(1e-4f));
        }

        [Test]
        public void Extract_Degenerate_IsIdentity()
        {
            var flat = float3x3.Scale(1f, 0f, 1f);
            AssertSameRotation(quaternion.identity, JacobianRotation.Extract(flat));
        }
    }
}
