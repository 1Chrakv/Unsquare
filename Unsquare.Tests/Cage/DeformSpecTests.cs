using NUnit.Framework;
using Unity.Mathematics;
using Unsquare.Core.Cage;

namespace Unsquare.Tests.Cage
{
    public class DeformSpecTests
    {
        private static readonly CageOffsets Raised =
            CageOffsets.Identity.Move(CageHandle.Face(3), new float3(0f, 2f, 0f));

        private static readonly CageOffsets Widened =
            CageOffsets.Identity.Move(CageHandle.Face(1), new float3(1f, 0f, 0f));

        [Test]
        public void Equality_IncludesFlag()
        {
            var lifted = new DeformSpec(Raised, false);
            Assert.That(new DeformSpec(CageOffsets.FromArray(Raised.ToArray()), false), Is.EqualTo(lifted));
            Assert.That(lifted.WithFlatTerrain(true), Is.Not.EqualTo(lifted));
            Assert.That(lifted.WithLotOffsets(CageOffsets.Identity), Is.Not.EqualTo(lifted));
        }

        [Test]
        public void Equality_IncludesBuildingCage()
        {
            var linked = new DeformSpec(Raised, false);
            var split = linked.WithBuildingOffsets(Widened);
            Assert.That(split, Is.Not.EqualTo(linked));
            Assert.That(new DeformSpec(Raised, Widened, false), Is.EqualTo(split));
        }

        [Test]
        public void With_KeepsTheOtherParts()
        {
            var flat = new DeformSpec(Raised, Widened, true);
            Assert.That(flat.WithLotOffsets(CageOffsets.Identity).FlatTerrain, Is.True);
            Assert.That(flat.WithLotOffsets(CageOffsets.Identity).BuildingOffsets, Is.EqualTo(Widened));
            Assert.That(flat.WithBuildingOffsets(CageOffsets.Identity).LotOffsets, Is.EqualTo(Raised));
            Assert.That(flat.WithFlatTerrain(false).LotOffsets, Is.EqualTo(Raised));
        }

        [Test]
        public void TwoArgumentConstructor_IsLinked()
        {
            var spec = new DeformSpec(Raised, false);
            Assert.That(spec.IsLinked, Is.True);
            Assert.That(spec.BuildingOffsets, Is.EqualTo(Raised));
            Assert.That(spec.WithBuildingOffsets(Widened).IsLinked, Is.False);
        }

        [Test]
        public void Identity_IsLiftedAndUndeformed()
        {
            Assert.That(DeformSpec.Identity.IsIdentity, Is.True);
            Assert.That(DeformSpec.Identity.IsLinked, Is.True);
            Assert.That(DeformSpec.Identity.FlatTerrain, Is.False);
        }

        [Test]
        public void Normalized_UndeformedFlat_IsIdentity()
        {
            Assert.That(new DeformSpec(CageOffsets.Identity, true).Normalized(), Is.SameAs(DeformSpec.Identity));
        }

        [Test]
        public void Normalized_FlatWithUndeformedLot_DropsFlag()
        {
            var spec = new DeformSpec(CageOffsets.Identity, Widened, true).Normalized();
            Assert.That(spec.FlatTerrain, Is.False);
            Assert.That(spec.BuildingOffsets, Is.EqualTo(Widened));
        }

        [Test]
        public void Equality_IncludesBlendMode()
        {
            var split = new DeformSpec(Raised, Widened, false);
            Assert.That(split.WithBlendMode(BlendMode.All), Is.Not.EqualTo(split));
            Assert.That(split.WithBlendMode(BlendMode.All).WithLotOffsets(Widened).BlendMode, Is.EqualTo(BlendMode.All));
        }

        [Test]
        public void Normalized_Linked_KeepsBlendMode()
        {
            Assert.That(new DeformSpec(Raised, Raised, false, BlendMode.All).Normalized().BlendMode,
                Is.EqualTo(BlendMode.All));
            Assert.That(new DeformSpec(CageOffsets.Identity, CageOffsets.Identity, false, BlendMode.All).Normalized()
                .BlendMode, Is.EqualTo(BlendMode.All));
        }

        [Test]
        public void ForVariant_Linked_BlendsNothing()
        {
            Assert.That(new DeformSpec(Raised, Raised, false, BlendMode.All).ForVariant().BlendMode,
                Is.EqualTo(BlendMode.None));
            Assert.That(new DeformSpec(CageOffsets.Identity, CageOffsets.Identity, true, BlendMode.All).ForVariant(),
                Is.SameAs(DeformSpec.Identity));
            Assert.That(new DeformSpec(Raised, Widened, false, BlendMode.All).ForVariant().BlendMode,
                Is.EqualTo(BlendMode.All));
        }

        [Test]
        public void Normalized_DeformedLot_KeepsFlag()
        {
            var spec = new DeformSpec(Raised, Widened, true);
            Assert.That(spec.Normalized(), Is.SameAs(spec));
        }
    }
}
