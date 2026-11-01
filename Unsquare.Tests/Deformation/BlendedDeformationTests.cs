using NUnit.Framework;
using Unity.Mathematics;
using Unsquare.Core.Cage;
using Unsquare.Core.Deformation;
using Unsquare.Core.Editing;

namespace Unsquare.Tests.Deformation
{
    public class BlendedDeformationTests
    {
        private static readonly CageBox Lot = new(new float3(-24, 0, -24), new float3(24, 12, 24));
        private static readonly CageBox House = new(new float3(-5, 0, -6), new float3(5, 12, 6));

        private static readonly DeformSpec Linked =
            new(CageOffsets.Identity.Move(CageHandle.Face(3), new float3(0f, 1f, 0f)), false);

        // The house grown by 3 m over its garden (+x side), the lot unchanged.
        private static DeformSpec Grown()
        {
            var view = SplitCageEdit.View(Lot, CageOffsets.Identity, House);
            return SplitCageEdit.EditBuilding(Lot, House, DeformSpec.Identity, view,
                view.Move(CageHandle.Face(1), new float3(3f, 0f, 0f)));
        }

        [Test]
        public void Create_Linked_SharesOneDeformation()
        {
            var deformations = SplitDeformations.Create(Lot, House, Linked);
            Assert.That(deformations.Building, Is.SameAs(deformations.Lot));
            Assert.That(deformations.Blend, Is.SameAs(deformations.Lot));
        }

        [Test]
        public void Apply_InsideFootprint_EqualsBuilding()
        {
            var deformations = SplitDeformations.Create(Lot, House, Grown());
            var p = new float3(4f, 0f, 2f);
            Assert.That(deformations.Blend.Apply(p, out _), Is.EqualTo(deformations.Building.Apply(p, out _)));
        }

        [Test]
        public void Apply_BeyondMargin_EqualsLot()
        {
            var spec = Grown();
            var deformations = SplitDeformations.Create(Lot, House, spec);
            float margin = CageBlend.Margin(Lot, House, spec);
            var p = new float3(House.Max.x + margin + 0.5f, 0f, 0f);
            Assert.That(deformations.Blend.Apply(p, out _), Is.EqualTo(deformations.Lot.Apply(p, out _)));
        }

        [Test]
        public void Apply_AcrossRing_IsContinuous()
        {
            var blend = SplitDeformations.Create(Lot, House, Grown()).Blend;
            float3 previous = blend.Apply(new float3(0f, 0f, 1f), out _);
            for (float x = 0.05f; x < 24f; x += 0.05f)
            {
                float3 next = blend.Apply(new float3(x, 0f, 1f), out _);
                Assert.That(math.distance(next, previous), Is.LessThan(0.2f), $"x {x}");
                previous = next;
            }
        }

        [TestCase(7f, 0f, 1f)]
        [TestCase(8.5f, 0f, 7.5f)]
        [TestCase(-6.5f, 3f, -7f)]
        public void Apply_Jacobian_MatchesFiniteDifferences(float x, float y, float z)
        {
            var blend = SplitDeformations.Create(Lot, House, Grown()).Blend;
            var p = new float3(x, y, z);
            blend.Apply(p, out float3x3 jacobian);
            const float h = 1e-2f;
            for (int axis = 0; axis < 3; axis++)
            {
                float3 step = float3.zero;
                step[axis] = h;
                float3 numeric = (blend.Apply(p + step, out _) - blend.Apply(p - step, out _)) / (2f * h);
                Assert.That(math.distance(numeric, jacobian[axis]), Is.LessThan(2e-2f), $"axis {axis}");
            }
        }

        [Test]
        public void Margin_Linked_IsMinimum()
        {
            Assert.That(CageBlend.Margin(Lot, House, Linked), Is.EqualTo(CageBlend.MarginMeters));
        }

        [Test]
        public void Margin_GrownBuilding_GrowsWithDifference()
        {
            Assert.That(CageBlend.Margin(Lot, House, Grown()), Is.GreaterThanOrEqualTo(6f));
        }

        [Test]
        public void ValidateSplit_BuildingGrownOverGarden_IsValid()
        {
            Assert.That(CageValidator.ValidateSplit(Lot, House, Grown(), CageLimits.Default),
                Is.EqualTo(CageValidity.Valid));
        }

        [Test]
        public void ValidateSplit_Linked_MatchesSingleCage()
        {
            Assert.That(CageValidator.ValidateSplit(Lot, House, Linked, CageLimits.Default),
                Is.EqualTo(CageValidator.Validate(Lot, Linked.LotOffsets, CageLimits.Default)));
        }

        [Test]
        public void ValidateSplit_BuildingFoldedOnItsBox_IsInverted()
        {
            var view = SplitCageEdit.View(Lot, CageOffsets.Identity, House);
            var folded = SplitCageEdit.EditBuilding(Lot, House, DeformSpec.Identity, view,
                view.Move(CageHandle.Face(3), new float3(0f, -14f, 0f)));
            Assert.That(CageValidator.ValidateSplit(Lot, House, folded, CageLimits.Default),
                Is.Not.EqualTo(CageValidity.Valid));
        }

        [Test]
        public void TryInvert_Blend_RoundTripsRingPoints()
        {
            var blend = SplitDeformations.Create(Lot, House, Grown()).Blend;
            foreach (var p in new[] { new float3(7f, 0f, 1f), new float3(-7f, 0f, -8f), new float3(10f, 0f, 9f) })
            {
                Assert.That(DeformationInverse.TryInvert(blend, blend.Apply(p, out _), out var back), Is.True);
                Assert.That(math.distance(back, p), Is.LessThan(1e-2f));
            }
        }
    }
}
