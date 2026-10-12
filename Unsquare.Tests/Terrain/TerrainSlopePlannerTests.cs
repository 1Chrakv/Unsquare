using NUnit.Framework;
using Unity.Mathematics;
using Unsquare.Core.Cage;
using Unsquare.Core.Terrain;

namespace Unsquare.Tests.Terrain
{
    public class TerrainSlopePlannerTests
    {
        private static readonly CageBox Lot = new(new float3(-8, 0, -12), new float3(8, 15, 12));

        [Test]
        public void Plan_Identity_IsEmpty()
        {
            Assert.That(TerrainSlopePlanner.Plan(Lot, CageOffsets.Identity).IsEmpty, Is.True);
        }

        [Test]
        public void Plan_HorizontalMoveOnly_IsEmpty()
        {
            var offsets = CageOffsets.Identity.Move(CageHandle.Corner(5), new float3(2f, 0f, -1.5f));
            Assert.That(TerrainSlopePlanner.Plan(Lot, offsets).IsEmpty, Is.True);
        }

        [Test]
        public void Plan_UniformRaise_PadOnlyEdgesZero()
        {
            var offsets = CageOffsets.Identity.Move(CageHandle.Face(2), new float3(0f, 3f, 0f));
            var slope = TerrainSlopePlanner.Plan(Lot, offsets);
            Assert.That(slope.PadOffset, Is.EqualTo(3f).Within(1e-4f));
            Assert.That(slope.EdgeLift.IsZero, Is.True);
        }

        [Test]
        public void Plan_OneCornerRaised_PadIsCentreLift()
        {
            // Corner 0 is the ground corner at (-x, -z); the lot centre gets a quarter of its lift.
            var offsets = CageOffsets.Identity.Move(CageHandle.Corner(0), new float3(0f, 4f, 0f));
            Assert.That(TerrainSlopePlanner.Plan(Lot, offsets).PadOffset, Is.EqualTo(1f).Within(1e-4f));
        }

        [Test]
        public void Plan_EdgesRelativeToCentre()
        {
            var offsets = CageOffsets.Identity
                .Move(CageHandle.Corner(0), new float3(0f, 6f, 0f))
                .Move(CageHandle.Corner(1), new float3(0f, 2f, 0f));
            var slope = TerrainSlopePlanner.Plan(Lot, offsets);
            Assert.That(slope.PadOffset, Is.EqualTo(2f).Within(1e-4f));
            Assert.That(slope.EdgeLift, Is.EqualTo(GroundLift.FromCage(Lot, offsets).Minus(2f)));
        }

        [Test]
        public void Plan_NullOffsets_Throws()
        {
            Assert.That(() => TerrainSlopePlanner.Plan(Lot, null),
                Throws.InstanceOf<System.ArgumentNullException>());
        }
    }
}
