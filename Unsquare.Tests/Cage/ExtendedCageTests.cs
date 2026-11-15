using NUnit.Framework;
using Unity.Mathematics;
using Unsquare.Core.Cage;
using Unsquare.Core.Deformation;

namespace Unsquare.Tests.Cage
{
    public class ExtendedCageTests
    {
        private static readonly CageBox Lot = new(new float3(-12, 0, -16), new float3(12, 20, 16));
        private static readonly CageBox Edit = ExtendedCage.RaiseTop(Lot, 24f);

        private static CageOffsets Skewed() => CageOffsets.Identity
            .Move(CageHandle.Corner(7), new float3(2.3f, 1.7f, -0.9f))
            .Move(CageHandle.Corner(2), new float3(-1.1f, 0.4f, 0.6f))
            .Move(CageHandle.Face(3), new float3(0.7f, 0.3f, 1.2f))
            .Move(CageHandle.Corner(1), new float3(0.5f, -0.3f, 0.2f));

        [Test]
        public void RaiseTop_LowerTop_KeepsBox()
        {
            Assert.That(ExtendedCage.RaiseTop(Lot, 15f), Is.EqualTo(Lot));
        }

        [Test]
        public void RaiseTop_HigherTop_RaisesOnlyMaxY()
        {
            Assert.That(Edit.Min, Is.EqualTo(Lot.Min));
            Assert.That(Edit.Max, Is.EqualTo(new float3(12, 24, 16)));
        }

        [Test]
        public void Enclose_DisjointBoxes_CoversBoth()
        {
            var wing = new CageBox(new float3(-20, 0, -4), new float3(-14, 26, 4));
            var box = ExtendedCage.Enclose(Lot, wing);
            Assert.That(box.Min, Is.EqualTo(new float3(-20, 0, -16)));
            Assert.That(box.Max, Is.EqualTo(new float3(12, 26, 16)));
        }

        [Test]
        public void Enclose_Contained_IsUnchanged()
        {
            var inner = new CageBox(new float3(-3, 0, -4), new float3(3, 9, 4));
            Assert.That(ExtendedCage.Enclose(Lot, inner), Is.EqualTo(Lot));
        }

        [Test]
        public void ConvertDelta_Negated_GivesExactNegation()
        {
            var building = new CageBox(new float3(-3, 0, -4), new float3(3, 9, 4));
            var delta = Skewed();
            var negated = CageOffsets.Identity.Minus(delta);
            var forward = ExtendedCage.ConvertDelta(building, delta, Lot);
            var backward = ExtendedCage.ConvertDelta(building, negated, Lot);
            Assert.That(forward.Plus(backward).IsIdentity, Is.True);
            Assert.That(forward.IsIdentity, Is.False);
        }

        [Test]
        public void ConvertDelta_FromIdentity_MatchesConvert()
        {
            var offsets = Skewed();
            var converted = ExtendedCage.Convert(Lot, offsets, Edit);
            var delta = ExtendedCage.ConvertDelta(Lot, offsets, Edit);
            for (int i = 0; i < CageTopology.CornerCount; i++)
                Assert.That(math.distance(delta.Meters(i), converted.Meters(i)), Is.LessThanOrEqualTo(0.11f));
        }

        [Test]
        public void Convert_Identity_StaysIdentity()
        {
            Assert.That(ExtendedCage.Convert(Lot, CageOffsets.Identity, Edit).IsIdentity, Is.True);
        }

        [Test]
        public void Convert_SameBox_ReturnsInput()
        {
            var offsets = Skewed();
            Assert.That(ExtendedCage.Convert(Lot, offsets, Lot), Is.SameAs(offsets));
        }

        [Test]
        public void Convert_LotToEditAndBack_GivesLotOffsets()
        {
            var lot = Skewed();
            var edit = ExtendedCage.Convert(Lot, lot, Edit);
            Assert.That(ExtendedCage.Convert(Edit, edit, Lot), Is.EqualTo(lot));
        }

        [Test]
        public void Convert_RaisedTop_KeepsBottomCorners()
        {
            var lot = Skewed();
            var edit = ExtendedCage.Convert(Lot, lot, Edit);
            for (int corner = 0; corner < CageTopology.CornerCount; corner++)
            {
                if (CageTopology.CornerSide(corner).y == 0)
                    Assert.That(edit.Steps(corner), Is.EqualTo(lot.Steps(corner)), $"corner {corner}");
            }
        }

        [Test]
        public void Convert_EditCage_DeformsPointsAboveTheLotLikeTheLotCage()
        {
            var lot = Skewed();
            var edit = ExtendedCage.Convert(Lot, lot, Edit);
            var lotDeformation = new TrilinearCageDeformation(Lot, lot);
            var editDeformation = new TrilinearCageDeformation(Edit, edit);
            foreach (var p in new[] { new float3(3, 22, -5), new float3(-11, 24, 15), new float3(0, 10, 0) })
            {
                float error = math.distance(editDeformation.Apply(p, out _), lotDeformation.Apply(p, out _));
                Assert.That(error, Is.LessThan(CageOffsets.StepMeters), $"at {p}");
            }
        }
    }
}
