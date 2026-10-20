using NUnit.Framework;
using Unity.Mathematics;
using Unsquare.Core.Deformation;

namespace Unsquare.Tests.Deformation
{
    public class OctahedralPackingTests
    {
        private static readonly float3[] Directions =
        {
            new(0f, 0f, 1f), new(0f, 0f, -1f), new(1f, 0f, 0f), new(-1f, 0f, 0f), new(0f, 1f, 0f),
            new(0f, -1f, 0f), math.normalize(new float3(1f, 2f, 3f)), math.normalize(new float3(-3f, 1f, -2f)),
            math.normalize(new float3(0.2f, -0.7f, -0.1f)), math.normalize(new float3(-1f, -1f, 1f)),
        };

        [Test]
        public void PackNormal_RoundTrip_KeepsDirection()
        {
            foreach (var n in Directions)
                Assert.That(math.dot(OctahedralPacking.UnpackNormal(OctahedralPacking.PackNormal(n)), n),
                    Is.GreaterThan(0.99999f), $"{n}");
        }

        [Test]
        public void PackTangent_RoundTrip_KeepsDirectionAndHandedness()
        {
            foreach (var d in Directions)
            {
                foreach (float w in new[] { 1f, -1f })
                {
                    float4 t = OctahedralPacking.UnpackTangent(OctahedralPacking.PackTangent(new float4(d, w)));
                    Assert.That(math.dot(t.xyz, d), Is.GreaterThan(0.9999f), $"{d}");
                    Assert.That(t.w, Is.EqualTo(w));
                }
            }
        }

        [Test]
        public void PackNormal_UpAndRight_MatchGameBitLayout()
        {
            Assert.That(OctahedralPacking.PackNormal(new float3(0f, 0f, 1f)), Is.EqualTo(0u));
            Assert.That(OctahedralPacking.PackNormal(new float3(1f, 0f, 0f)), Is.EqualTo(0x7FFFu));
            Assert.That(OctahedralPacking.PackNormal(new float3(0f, -1f, 0f)), Is.EqualTo(0x80010000u));
        }

        [Test]
        public void PackTangent_NegativeHandedness_SetsSignBitOnly()
        {
            var t = new float4(1f, 0f, 0f, 1f);
            uint right = OctahedralPacking.PackTangent(t);
            Assert.That(right, Is.EqualTo(0x3FFFu));
            Assert.That(OctahedralPacking.PackTangent(new float4(t.xyz, -1f)), Is.EqualTo(right | 0x80000000u));
        }

        [Test]
        public void Encode_LowerHemisphere_FoldsIntoCorners()
        {
            float2 e = OctahedralPacking.Encode(math.normalize(new float3(0.1f, 0.1f, -1f)));
            Assert.That(math.abs(e.x) + math.abs(e.y), Is.GreaterThan(1f));
        }

        [Test]
        public void Encode_ZeroVector_ReturnsZero()
        {
            Assert.That(OctahedralPacking.Encode(float3.zero), Is.EqualTo(float2.zero));
        }
    }
}
