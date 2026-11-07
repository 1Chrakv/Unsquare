using NUnit.Framework;
using Unity.Mathematics;
using Unsquare.Core.Cage;
using Unsquare.Core.Editing;
using Unsquare.Core.Terrain;

namespace Unsquare.Tests.Terrain
{
    public class BuildingSeatTests
    {
        private static readonly CageBox Lot = new(new float3(-24, 0, -24), new float3(24, 12, 24));
        private static readonly CageBox House = new(new float3(-5, 0, -6), new float3(5, 12, 6));

        // The lot's back half lifted by 2 m (back columns up), the house left as it was.
        private static readonly CageOffsets LiftedBack = CageOffsets.Identity
            .Move(CageHandle.Corner(0), new float3(0f, 2f, 0f)).Move(CageHandle.Corner(2), new float3(0f, 2f, 0f))
            .Move(CageHandle.Corner(1), new float3(0f, 2f, 0f)).Move(CageHandle.Corner(3), new float3(0f, 2f, 0f));

        [Test]
        public void MaxOffGround_Linked_IsZero()
        {
            Assert.That(BuildingSeat.MaxOffGround(Lot, House, new DeformSpec(LiftedBack, false)), Is.EqualTo(0f));
        }

        [Test]
        public void IsOffGround_LotLiftedUnderStillHouse_IsTrue()
        {
            var spec = new DeformSpec(LiftedBack, CageOffsets.Identity, false);
            Assert.That(BuildingSeat.IsOffGround(Lot, House, spec), Is.True);
        }

        [Test]
        public void TargetHeights_FitAsChange_SeatsHouse()
        {
            var spec = new DeformSpec(LiftedBack, CageOffsets.Identity, false);
            var view = SplitCageEdit.View(Lot, spec.BuildingOffsets, House);
            var heights = BuildingSeat.TargetHeights(Lot, spec.LotOffsets, House);
            var fitted = GroundFit.Fit(House, view, heights, GroundFitMode.AllCorners);

            var seated = SplitCageEdit.EditBuilding(Lot, House, spec, view, fitted);

            Assert.That(BuildingSeat.IsOffGround(Lot, House, seated), Is.False);
        }
    }
}
