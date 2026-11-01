using NUnit.Framework;
using Unity.Mathematics;
using Unsquare.Core.Cage;
using Unsquare.Core.Deformation;
using Unsquare.Core.Editing;

namespace Unsquare.Tests.Editing
{
    public class SplitCageEditTests
    {
        // A small house on a large lot: building-box changes are amplified on the lot box.
        private static readonly CageBox Lot = new(new float3(-24, 0, -24), new float3(24, 12, 24));
        private static readonly CageBox House = new(new float3(-5, 0, -6), new float3(5, 12, 6));

        private static readonly CageOffsets Raised =
            CageOffsets.Identity.Move(CageHandle.Face(3), new float3(0f, 1f, 0f));

        private static readonly DeformSpec Linked = new(Raised, false);

        private static CageOffsets ViewOf(DeformSpec spec) =>
            SplitCageEdit.View(Lot, spec.BuildingOffsets, House);

        private static DeformSpec Nudge(DeformSpec spec, CageHandle handle, float3 delta)
        {
            var view = ViewOf(spec);
            return SplitCageEdit.EditBuilding(Lot, House, spec, view, view.Move(handle, delta));
        }

        [Test]
        public void EditBuilding_NudgeAndBack_ReturnsExactly()
        {
            var nudged = Nudge(Linked, CageHandle.Corner(3), new float3(0.1f, 0f, 0f));
            Assert.That(nudged.IsLinked, Is.False);

            var back = Nudge(nudged, CageHandle.Corner(3), new float3(-0.1f, 0f, 0f));
            Assert.That(back, Is.EqualTo(Linked));
        }

        [Test]
        public void EditBuilding_ManyNudgesUpAndDown_ReturnToStart()
        {
            var spec = Linked;
            for (int i = 0; i < 5; i++)
                spec = Nudge(spec, CageHandle.Face(1), new float3(0.1f, 0f, 0f));
            for (int i = 0; i < 5; i++)
                spec = Nudge(spec, CageHandle.Face(1), new float3(-0.1f, 0f, 0f));
            Assert.That(spec, Is.EqualTo(Linked));
        }

        [Test]
        public void EditBuilding_Drag_MovesViewByTotalDelta()
        {
            var startView = ViewOf(Linked);
            var spec = SplitCageEdit.EditBuilding(Lot, House, Linked, startView,
                startView.Move(CageHandle.Face(1), new float3(2f, 0f, 0f)));
            var view = ViewOf(spec);
            for (int i = 0; i < CageTopology.CornerCount; i++)
            {
                float expected = (i & 1) != 0 ? 2f : 0f;
                Assert.That(view.Meters(i).x - startView.Meters(i).x, Is.EqualTo(expected).Within(0.11f), $"corner {i}");
            }
        }

        [Test]
        public void EditBuilding_AbsoluteResult_IsAppliedAsChange()
        {
            var linkedView = ViewOf(Linked);
            var moved = Nudge(Linked, CageHandle.Corner(3), new float3(1.5f, 0f, 0f));
            var view = ViewOf(moved);

            // Like fit or align: an absolute corner value on the view, applied as a change, lands on the start.
            var restored = view.WithCornerSteps(3, linkedView.Steps(3));
            var spec = SplitCageEdit.EditBuilding(Lot, House, moved, view, restored);
            Assert.That(spec, Is.EqualTo(Linked));
        }

        [Test]
        public void Snap_DifferenceInvisibleOnBuilding_LinksCages()
        {
            // A far lot corner moves; on the house box the difference rounds away.
            var lot = Raised.Move(CageHandle.Corner(0), new float3(0.1f, 0f, 0f));
            var spec = SplitCageEdit.Snap(Lot, House, new DeformSpec(lot, Raised, false));
            Assert.That(spec.IsLinked, Is.True);
            Assert.That(spec.LotOffsets, Is.EqualTo(lot));
        }

        [Test]
        public void Snap_BuildingUndeformedOnItsBox_ResetsBuildingCage()
        {
            var building = CageOffsets.Identity.Move(CageHandle.Corner(0), new float3(0.1f, 0f, 0f));
            var spec = SplitCageEdit.Snap(Lot, House, new DeformSpec(Raised.Move(CageHandle.Face(3),
                new float3(0f, 2f, 0f)), building, false));
            Assert.That(spec.BuildingOffsets.IsIdentity, Is.True);
        }

        [Test]
        public void EditBoth_SplitStart_KeepsBuildingShape()
        {
            var split = Nudge(Linked, CageHandle.Corner(3), new float3(1f, 0f, 0f));
            var newLot = split.LotOffsets.Move(CageHandle.Face(5), new float3(0f, 0f, 2f));

            var spec = SplitCageEdit.EditBoth(Lot, House, split, newLot);

            Assert.That(spec.LotOffsets, Is.EqualTo(newLot));
            Assert.That(spec.BuildingOffsets.Minus(spec.LotOffsets),
                Is.EqualTo(split.BuildingOffsets.Minus(split.LotOffsets)));
        }

        [Test]
        public void EditBoth_LinkedStart_StaysLinked()
        {
            var newLot = Raised.Move(CageHandle.Face(5), new float3(0f, 0f, 2f));
            var spec = SplitCageEdit.EditBoth(Lot, House, Linked, newLot);
            Assert.That(spec.IsLinked, Is.True);
            Assert.That(spec.LotOffsets, Is.EqualTo(newLot));
        }

        [Test]
        public void EditLot_LeavesBuildingCage()
        {
            var newLot = Raised.Move(CageHandle.Face(5), new float3(0f, 0f, 2f));
            var spec = SplitCageEdit.EditLot(Lot, House, Linked, newLot);
            Assert.That(spec.LotOffsets, Is.EqualTo(newLot));
            Assert.That(spec.BuildingOffsets, Is.EqualTo(Raised));
        }

        [Test]
        public void Reset_PerTarget_ResetsOnlyThatCage()
        {
            var split = Nudge(Linked, CageHandle.Corner(3), new float3(1f, 0f, 0f));

            var building = SplitCageEdit.Reset(Lot, House, split, CageTarget.Building);
            Assert.That(building.BuildingOffsets.IsIdentity, Is.True);
            Assert.That(building.LotOffsets, Is.EqualTo(Raised));

            var lot = SplitCageEdit.Reset(Lot, House, split, CageTarget.Lot);
            Assert.That(lot.LotOffsets.IsIdentity, Is.True);
            Assert.That(lot.BuildingOffsets, Is.EqualTo(split.BuildingOffsets));

            Assert.That(SplitCageEdit.Reset(Lot, House, split, CageTarget.Both), Is.SameAs(DeformSpec.Identity));
        }

        [Test]
        public void Link_CopiesLotToBuilding()
        {
            var split = Nudge(Linked, CageHandle.Corner(3), new float3(1f, 0f, 0f));
            Assert.That(SplitCageEdit.Link(split), Is.EqualTo(Linked));
        }

        [Test]
        public void ClampBuilding_BeyondHouseLimit_ClampsOnHouseBox()
        {
            var limits = CageLimits.Default;
            var spec = Nudge(DeformSpec.Identity, CageHandle.Corner(1), new float3(30f, 0f, 0f));

            var clamped = SplitCageEdit.ClampBuilding(Lot, House, spec, limits);

            float limit = CageValidator.OffsetLimit(House, limits).x;
            Assert.That(ViewOf(clamped).Meters(1).x, Is.LessThanOrEqualTo(limit + 0.11f));
            Assert.That(ViewOf(clamped).Meters(1).x, Is.GreaterThan(limit - 0.6f));
        }

        [Test]
        public void ClampBuilding_Linked_IsUnchanged()
        {
            Assert.That(SplitCageEdit.ClampBuilding(Lot, House, Linked, CageLimits.Default), Is.SameAs(Linked));
        }
    }
}
