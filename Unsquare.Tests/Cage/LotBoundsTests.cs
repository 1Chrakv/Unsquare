using NUnit.Framework;
using Unity.Mathematics;
using Unsquare.Core.Cage;

namespace Unsquare.Tests.Cage
{
    public class LotBoundsTests
    {
        private static readonly CageBox Lot = new(new float3(-8, 0, -12), new float3(8, 9, 12));

        [Test]
        public void ExceedsLot_Identity_IsInside()
        {
            Assert.That(LotBounds.ExceedsLot(Lot, CageOffsets.Identity), Is.False);
        }

        [Test]
        public void ExceedsLot_InsetBottomCorner_IsInside()
        {
            var offsets = CageOffsets.Identity.Move(CageHandle.Corner(0), new float3(2, 0, 3));
            Assert.That(LotBounds.ExceedsLot(Lot, offsets), Is.False);
        }

        [Test]
        public void ExceedsLot_PushedOutBottomCorner_Exceeds()
        {
            var offsets = CageOffsets.Identity.Move(CageHandle.Corner(5), new float3(1, 0, 0));
            Assert.That(LotBounds.ExceedsLot(Lot, offsets), Is.True);
        }

        [Test]
        public void ExceedsLot_MovedTopFaceOnly_NeverExceeds()
        {
            var offsets = CageOffsets.Identity.Move(CageHandle.Face(3), new float3(6, 4, -5));
            Assert.That(LotBounds.ExceedsLot(Lot, offsets), Is.False);
        }
    }
}
