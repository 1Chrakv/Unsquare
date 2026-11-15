using NUnit.Framework;
using Unity.Mathematics;
using Unsquare.Core.Cage;

namespace Unsquare.Tests.Cage
{
    public class LevelUpCarryTests
    {
        private static readonly CageBox Low = new(new float3(-8f, 0f, -8f), new float3(8f, 10f, 8f));
        private static readonly CageBox High = new(new float3(-8f, 0f, -8f), new float3(8f, 20f, 8f));

        private static readonly CageHandle Top = CageHandle.Face(3);
        private static readonly CageHandle Right = CageHandle.Face(1);

        [Test]
        public void Rescale_Identity_StaysIdentity()
        {
            Assert.That(LevelUpCarry.Rescale(DeformSpec.Identity, Low, High), Is.EqualTo(DeformSpec.Identity));
        }

        [Test]
        public void Rescale_SameHeight_ReturnsEqualSpec()
        {
            var spec = new DeformSpec(CageOffsets.Identity.Move(Top, new float3(1.3f, 0.7f, 0f)), false);
            Assert.That(LevelUpCarry.Rescale(spec, Low, Low), Is.EqualTo(spec));
        }

        [Test]
        public void Rescale_PlanShape_IsUnchanged()
        {
            var spec = new DeformSpec(CageOffsets.Identity.Move(Right, new float3(2f, 0f, -1f)), false);
            Assert.That(LevelUpCarry.Rescale(spec, Low, High), Is.EqualTo(spec));
        }

        [Test]
        public void Rescale_LeanAndRoofRaise_ScaleWithHeight()
        {
            var spec = new DeformSpec(CageOffsets.Identity.Move(Top, new float3(2f, 1f, 0f)), false);
            var expected = new DeformSpec(CageOffsets.Identity.Move(Top, new float3(4f, 2f, 0f)), false);
            Assert.That(LevelUpCarry.Rescale(spec, Low, High), Is.EqualTo(expected));
        }

        [Test]
        public void Rescale_TopOverMovedBottom_ScalesOnlyTheRise()
        {
            // Whole cage shifted 3 m, top leaned 1 m further: the shift stays, the lean doubles.
            var offsets = CageOffsets.Identity.Move(CageHandle.Cage(), new float3(3f, 0f, 0f))
                .Move(Top, new float3(1f, 0f, 0f));
            var expected = CageOffsets.Identity.Move(CageHandle.Cage(), new float3(3f, 0f, 0f))
                .Move(Top, new float3(2f, 0f, 0f));
            var result = LevelUpCarry.Rescale(new DeformSpec(offsets, false), Low, High);
            Assert.That(result.LotOffsets, Is.EqualTo(expected));
        }

        [Test]
        public void Rescale_OddRatio_RoundsToNearestStep()
        {
            var spec = new DeformSpec(CageOffsets.Identity.Move(Top, new float3(0.7f, 0f, 0f)), false);
            var taller = new CageBox(Low.Min, new float3(8f, 13f, 8f));
            var result = LevelUpCarry.Rescale(spec, Low, taller);
            Assert.That(result.LotOffsets.Steps(2), Is.EqualTo(new int3(9, 0, 0)));
        }

        [Test]
        public void Rescale_Linked_StaysLinked()
        {
            var spec = new DeformSpec(CageOffsets.Identity.Move(Top, new float3(0.3f, 0f, 0f)), false);
            var taller = new CageBox(Low.Min, new float3(8f, 15f, 8f));
            Assert.That(LevelUpCarry.Rescale(spec, Low, taller).IsLinked, Is.True);
        }

        [Test]
        public void Rescale_SplitCages_ScalesEachCage()
        {
            var lot = CageOffsets.Identity.Move(Right, new float3(1f, 0f, 0f));
            var building = CageOffsets.Identity.Move(Top, new float3(0f, 0f, 1.5f));
            var spec = new DeformSpec(lot, building, true, BlendMode.All);
            var result = LevelUpCarry.Rescale(spec, Low, High);
            Assert.That(result.LotOffsets, Is.EqualTo(lot));
            Assert.That(result.BuildingOffsets, Is.EqualTo(CageOffsets.Identity.Move(Top, new float3(0f, 0f, 3f))));
            Assert.That(result.FlatTerrain, Is.True);
            Assert.That(result.BlendMode, Is.EqualTo(BlendMode.All));
        }
    }
}
