using NUnit.Framework;
using Unity.Mathematics;
using Unsquare.Core.Cage;

namespace Unsquare.Tests.Cage
{
    public class CageShapeTests
    {
        private static readonly CageOffsets Raised = CageOffsets.Identity.Move(CageHandle.Face(3), new float3(0, 2, 0));

        [Test]
        public void Equals_IdentityInAnyScope_IsUndeformed()
        {
            var lotIdentity = new CageShape(CageScope.Lot, CageOffsets.Identity);
            Assert.That(lotIdentity, Is.EqualTo(CageShape.Undeformed));
            Assert.That(lotIdentity.GetHashCode(), Is.EqualTo(CageShape.Undeformed.GetHashCode()));
        }

        [Test]
        public void Equals_SameOffsetsDifferentScope_AreDifferent()
        {
            Assert.That(new CageShape(CageScope.Lot, Raised), Is.Not.EqualTo(new CageShape(CageScope.Building, Raised)));
        }

        [Test]
        public void Equals_SameScopeAndOffsets_AreEqual()
        {
            var copy = CageOffsets.FromArray(Raised.ToArray());
            Assert.That(new CageShape(CageScope.Lot, copy), Is.EqualTo(new CageShape(CageScope.Lot, Raised)));
        }

        [Test]
        public void Default_IsIdentity()
        {
            Assert.That(default(CageShape).IsIdentity, Is.True);
            Assert.That(default(CageShape), Is.EqualTo(CageShape.Undeformed));
        }
    }
}
