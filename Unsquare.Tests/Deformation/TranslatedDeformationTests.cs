using NUnit.Framework;
using Unity.Mathematics;
using Unsquare.Core.Cage;
using Unsquare.Core.Deformation;

namespace Unsquare.Tests.Deformation
{
    public class TranslatedDeformationTests
    {
        private static readonly CageBox Box = new(new float3(-6, 0, -8), new float3(6, 15, 8));
        private static readonly float3 Offset = new(1.5f, 12f, -2f);

        private static readonly CageOffsets Skewed = CageOffsets.Identity
            .Move(CageHandle.Corner(7), new float3(2f, 3f, -1f))
            .Move(CageHandle.Face(3), new float3(1.5f, 0f, 0.5f));

        [Test]
        public void Apply_IdentityInner_KeepsPoints()
        {
            var d = new TranslatedDeformation(new TrilinearCageDeformation(Box, CageOffsets.Identity), Offset);
            var p = new float3(0.3f, 2f, -1f);
            Assert.That(math.distance(d.Apply(p, out _), p), Is.LessThan(1e-4f));
        }

        [Test]
        public void Apply_MatchesInnerInBuildingSpace()
        {
            var inner = new TrilinearCageDeformation(Box, Skewed);
            var d = new TranslatedDeformation(inner, Offset);
            var local = new float3(-2f, 1f, 3f);

            var expected = inner.Apply(local + Offset, out var innerJacobian) - Offset;
            var actual = d.Apply(local, out var jacobian);

            Assert.That(math.distance(actual, expected), Is.LessThan(1e-4f));
            for (int c = 0; c < 3; c++)
                Assert.That(math.distance(jacobian[c], innerJacobian[c]), Is.LessThan(1e-5f));
        }

        [Test]
        public void Constructor_NullInner_Throws()
        {
            Assert.That(() => new TranslatedDeformation(null, Offset), Throws.ArgumentNullException);
        }
    }
}
