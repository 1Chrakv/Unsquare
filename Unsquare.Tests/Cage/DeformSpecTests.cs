using NUnit.Framework;
using Unity.Mathematics;
using Unsquare.Core.Cage;

namespace Unsquare.Tests.Cage
{
    public class DeformSpecTests
    {
        private static readonly CageOffsets Raised =
            CageOffsets.Identity.Move(CageHandle.Face(3), new float3(0f, 2f, 0f));

        [Test]
        public void Equality_IncludesFlag()
        {
            var lifted = new DeformSpec(Raised, false);
            Assert.That(new DeformSpec(CageOffsets.FromArray(Raised.ToArray()), false), Is.EqualTo(lifted));
            Assert.That(lifted.WithFlatTerrain(true), Is.Not.EqualTo(lifted));
            Assert.That(lifted.WithOffsets(CageOffsets.Identity), Is.Not.EqualTo(lifted));
        }

        [Test]
        public void With_KeepsTheOtherPart()
        {
            var flat = new DeformSpec(Raised, true);
            Assert.That(flat.WithOffsets(CageOffsets.Identity).FlatTerrain, Is.True);
            Assert.That(flat.WithFlatTerrain(false).Offsets, Is.EqualTo(Raised));
        }

        [Test]
        public void Identity_IsLiftedAndUndeformed()
        {
            Assert.That(DeformSpec.Identity.Offsets.IsIdentity, Is.True);
            Assert.That(DeformSpec.Identity.FlatTerrain, Is.False);
        }
    }
}
