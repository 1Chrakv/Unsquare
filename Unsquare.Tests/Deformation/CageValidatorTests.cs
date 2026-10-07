using NUnit.Framework;
using Unity.Mathematics;
using Unsquare.Core.Cage;
using Unsquare.Core.Deformation;

namespace Unsquare.Tests.Deformation
{
    public class CageValidatorTests
    {
        private static readonly CageBox Box = new(new float3(-5, 0, -5), new float3(5, 10, 5));

        [Test]
        public void Identity_IsValid()
        {
            Assert.That(CageValidator.Validate(Box, CageOffsets.Identity), Is.EqualTo(CageValidity.Valid));
        }

        [Test]
        public void ModerateTaper_IsValid()
        {
            var offsets = CageOffsets.Identity.Move(CageHandle.Edge(3), new float3(3, 0, 0));
            Assert.That(CageValidator.Validate(Box, offsets), Is.EqualTo(CageValidity.Valid));
        }

        [Test]
        public void TopFacePushedBelowBottom_IsInverted()
        {
            var offsets = CageOffsets.Identity.Move(CageHandle.Face(3), new float3(0, -12, 0));
            Assert.That(CageValidator.Validate(Box, offsets), Is.EqualTo(CageValidity.Inverted));
        }

        [Test]
        public void CornerPulledThroughOpposite_IsInverted()
        {
            var offsets = CageOffsets.Identity.Move(CageHandle.Corner(7), new float3(-11, 0, 0));
            Assert.That(CageValidator.Validate(Box, offsets), Is.EqualTo(CageValidity.Inverted));
        }

        [Test]
        public void HugeOffset_IsOutOfRange()
        {
            var offsets = CageOffsets.Identity.Move(CageHandle.Corner(0), new float3(0, 0, -25));
            Assert.That(CageValidator.Validate(Box, offsets), Is.EqualTo(CageValidity.OutOfRange));
        }

        [Test]
        public void Clamp_BringsOffsetsWithinLimit()
        {
            var offsets = CageOffsets.Identity.Move(CageHandle.Face(1), new float3(100, 0, 0));
            var clamped = CageValidator.Clamp(Box, offsets);
            Assert.That(clamped.Meters(1).x, Is.EqualTo(CageValidator.OffsetLimit(Box).x).Within(1e-4f));
            Assert.That(CageValidator.Validate(Box, clamped), Is.EqualTo(CageValidity.Valid));
        }
    }
}
