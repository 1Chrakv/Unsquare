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

        // A wide building whose +x wall is 2 m from the lot edge.
        private static readonly CageBox NearEdge = new(new float3(-5, 0, -6), new float3(22, 12, 6));

        private static readonly DeformSpec Linked =
            new(CageOffsets.Identity.Move(CageHandle.Face(3), new float3(0f, 1f, 0f)), false);

        private static DeformSpec Edit(CageBox house, CageHandle handle, float3 delta)
        {
            var view = SplitCageEdit.View(Lot, CageOffsets.Identity, house);
            return SplitCageEdit.EditBuilding(Lot, house, DeformSpec.Identity, view, view.Move(handle, delta));
        }

        // The house grown by 3 m over its garden (+x side), the lot unchanged.
        private static DeformSpec Grown() =>
            Edit(House, CageHandle.Face(1), new float3(3f, 0f, 0f)).WithBlendMode(BlendMode.All);

        // The front-right column pulled 3 m inwards (the in-game hospital case): extrapolated, this shears the garden.
        private static DeformSpec Tapered() =>
            Edit(House, CageHandle.Edge(EdgeOf(5, 7)), new float3(-3f, 0f, 0f)).WithBlendMode(BlendMode.All);

        private static int EdgeOf(int a, int b)
        {
            for (int i = 0; i < CageTopology.EdgeCount; i++)
            {
                var corners = CageTopology.CornersOf(CageHandle.Edge(i));
                if ((corners[0] == a && corners[1] == b) || (corners[0] == b && corners[1] == a))
                    return i;
            }
            throw new System.ArgumentException("No such edge.");
        }

        private static float3 Change(IDeformation deformation, IDeformation lot, float3 p) =>
            deformation.Apply(p, out _) - lot.Apply(p, out _);

        [Test]
        public void Create_Linked_SharesOneDeformation()
        {
            var deformations = SplitDeformations.Create(Lot, House, Linked);
            Assert.That(deformations.Building, Is.SameAs(deformations.Lot));
            Assert.That(deformations.Blend, Is.SameAs(deformations.Lot));
        }

        [TestCase(BlendMode.All, true)]
        [TestCase(BlendMode.None, false)]
        public void Create_BlendMode_PicksWhatGroundFollows(BlendMode mode, bool blends)
        {
            var d = SplitDeformations.Create(Lot, House, Grown().WithBlendMode(mode));
            Assert.That(d.Ground, Is.SameAs(blends ? d.Blend : d.Lot));
        }

        [Test]
        public void Apply_InsideFootprint_EqualsBuilding()
        {
            var deformations = SplitDeformations.Create(Lot, House, Grown());
            var p = new float3(4f, 0f, 2f);
            Assert.That(deformations.Blend.Apply(p, out _), Is.EqualTo(deformations.Building.Apply(p, out _)));
        }

        [Test]
        public void Apply_JustOutsideWall_TakesTheWallsChange()
        {
            var d = SplitDeformations.Create(Lot, House, Tapered());
            var wall = new float3(House.Max.x, 0f, 2f);
            var outside = wall + new float3(0.01f, 0f, 0f);
            float3 expected = Change(d.Building, d.Lot, wall);
            Assert.That(math.distance(Change(d.Blend, d.Lot, outside), expected), Is.LessThan(0.02f));
        }

        [Test]
        public void Apply_Ring_NeverMovesMoreThanTheWalls()
        {
            var d = SplitDeformations.Create(Lot, House, Tapered());
            float wallMax = 0f;
            for (int i = 0; i < CageTopology.CornerCount; i++)
                wallMax = math.max(wallMax, math.length(Change(d.Building, d.Lot, House.Corner(i))));

            for (float x = -24f; x <= 24f; x += 1f)
            for (float z = -24f; z <= 24f; z += 1f)
            {
                var p = new float3(x, 0f, z);
                if (math.all(p.xz >= House.Min.xz) && math.all(p.xz <= House.Max.xz))
                    continue;
                Assert.That(math.length(Change(d.Blend, d.Lot, p)), Is.LessThanOrEqualTo(wallMax + 1e-3f),
                    $"at {p}");
            }
        }

        [Test]
        public void Apply_WallNearLotEdge_EqualsLotAtTheEdge()
        {
            var spec = Edit(NearEdge, CageHandle.Corner(7), new float3(0f, 0f, 0.5f));
            var d = SplitDeformations.Create(Lot, NearEdge, spec);
            var edge = new float3(Lot.Max.x, 0f, 0f);
            Assert.That(d.Blend.Apply(edge, out _), Is.EqualTo(d.Lot.Apply(edge, out _)));
        }

        [Test]
        public void Apply_AcrossWallAndRing_IsContinuous()
        {
            var blend = SplitDeformations.Create(Lot, House, Tapered()).Blend;
            float3 previous = blend.Apply(new float3(0f, 0f, 1f), out _);
            for (float x = 0.05f; x < 24f; x += 0.05f)
            {
                float3 next = blend.Apply(new float3(x, 0f, 1f), out _);
                Assert.That(math.distance(next, previous), Is.LessThan(0.2f), $"x {x}");
                previous = next;
            }
        }

        [TestCase(7f, 0f, 1f)]
        [TestCase(6.5f, 0f, 7.5f)]
        [TestCase(-6.5f, 3f, -7f)]
        [TestCase(2f, 0f, 8f)]
        public void Apply_Jacobian_MatchesFiniteDifferences(float x, float y, float z)
        {
            var blend = SplitDeformations.Create(Lot, House, Tapered()).Blend;
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
        public void Ring_Linked_IsUniformMargin()
        {
            var ring = CageBlend.Ring(Lot, House, Linked);
            Assert.That(ring.MaxX, Is.EqualTo(CageBlend.MarginMeters));
        }

        [Test]
        public void Ring_GrownWithRoom_EndsAtTheLotEdge()
        {
            Assert.That(CageBlend.Ring(Lot, House, Grown()).MaxX, Is.EqualTo(Lot.Max.x - House.Max.x).Within(1e-4f));
        }

        [Test]
        public void Ring_WallNearLotEdge_EndsAtTheEdge()
        {
            var spec = Edit(NearEdge, CageHandle.Corner(7), new float3(0f, 0f, 0.5f));
            var ring = CageBlend.Ring(Lot, NearEdge, spec);
            Assert.That(ring.MaxX, Is.EqualTo(Lot.Max.x - NearEdge.Max.x).Within(1e-4f));
            Assert.That(ring.MinX, Is.EqualTo(NearEdge.Min.x - Lot.Min.x).Within(1e-4f));
        }

        [Test]
        public void Ring_GrownNearLotEdge_WidensPastTheEdgeToNotFold()
        {
            var spec = Edit(NearEdge, CageHandle.Face(1), new float3(3f, 0f, 0f)).WithBlendMode(BlendMode.All);
            Assert.That(CageBlend.Ring(Lot, NearEdge, spec).MaxX,
                Is.EqualTo(CageBlend.MarginPerMeter * 3f).Within(0.3f));
        }

        [Test]
        public void Ring_WallMovedInwards_EndsAtTheLotEdge()
        {
            var spec = Edit(NearEdge, CageHandle.Face(1), new float3(-10f, 0f, 0f)).WithBlendMode(BlendMode.All);
            Assert.That(CageBlend.Ring(Lot, NearEdge, spec).MaxX, Is.EqualTo(Lot.Max.x - NearEdge.Max.x).Within(1e-4f));
            var d = SplitDeformations.Create(Lot, NearEdge, spec);
            var edge = new float3(Lot.Max.x, 0f, 0f);
            Assert.That(d.Blend.Apply(edge, out _), Is.EqualTo(d.Lot.Apply(edge, out _)));
        }

        [Test]
        public void Ring_BuildingShiftedAlongZ_SpansToTheLotEdge()
        {
            var view = SplitCageEdit.View(Lot, CageOffsets.Identity, House);
            var shifted = view.Move(CageHandle.Face(4), new float3(0f, 0f, 5f))
                .Move(CageHandle.Face(5), new float3(0f, 0f, 5f));
            var spec = SplitCageEdit.EditBuilding(Lot, House, DeformSpec.Identity, view, shifted);
            var ring = CageBlend.Ring(Lot, House, spec);
            Assert.That(ring.MinX, Is.EqualTo(House.Min.x - Lot.Min.x).Within(1e-4f));
            Assert.That(ring.MaxX, Is.EqualTo(Lot.Max.x - House.Max.x).Within(1e-4f));
            Assert.That(ring.MinZ, Is.EqualTo(House.Min.z - Lot.Min.z).Within(1e-4f));
            Assert.That(ring.MaxZ, Is.EqualTo(Lot.Max.z - House.Max.z).Within(1e-4f));
        }

        [Test]
        public void Apply_NearLotEdge_StaysWithLot()
        {
            var spec = Edit(House, CageHandle.Face(1), new float3(-4f, 0f, 0f)).WithBlendMode(BlendMode.All);
            var d = SplitDeformations.Create(Lot, House, spec);
            var p = new float3(Lot.Max.x - 0.2f, 0f, 0f);
            float wall = math.length(Change(d.Building, d.Lot, new float3(House.Max.x, 0f, 0f)));
            Assert.That(math.length(Change(d.Blend, d.Lot, p)), Is.LessThan(0.01f * wall));
        }

        [TestCase(false, 3f, 3f)]
        [TestCase(false, -8f, 0f)]
        [TestCase(false, -4f, -4f)]
        [TestCase(false, 5f, -2f)]
        [TestCase(true, 3f, 3f)]
        [TestCase(true, 4f, 0f)]
        [TestCase(true, -8f, 2f)]
        public void Apply_ColumnMoved_RingNeverFolds(bool nearEdge, float dx, float dz)
        {
            var house = nearEdge ? NearEdge : House;
            var spec = Edit(house, CageHandle.Edge(EdgeOf(5, 7)), new float3(dx, 0f, dz)).WithBlendMode(BlendMode.All);
            var ring = CageBlend.Ring(Lot, house, spec);
            var blend = SplitDeformations.Create(Lot, house, spec).Blend;
            float2 min = house.Min.xz - new float2(ring.MinX, ring.MinZ);
            float2 max = house.Max.xz + new float2(ring.MaxX, ring.MaxZ);
            for (float x = min.x; x <= max.x; x += 0.25f)
            for (float z = min.y; z <= max.y; z += 0.25f)
            {
                var p = new float3(x, 0f, z);
                if (math.all(p.xz > house.Min.xz) && math.all(p.xz < house.Max.xz))
                    continue;
                blend.Apply(p, out float3x3 jacobian);
                Assert.That(math.determinant(jacobian), Is.GreaterThan(0.1f), $"at {p}");
            }
        }

        [Test]
        public void ValidateSplit_BuildingGrownOverGarden_IsValid()
        {
            Assert.That(CageValidator.ValidateSplit(Lot, House, Grown(), CageLimits.Default),
                Is.EqualTo(CageValidity.Valid));
        }

        [Test]
        public void ValidateSplit_GrownOverNearLotEdge_IsValid()
        {
            var spec = Edit(NearEdge, CageHandle.Face(1), new float3(3f, 0f, 0f));
            Assert.That(CageValidator.ValidateSplit(Lot, NearEdge, spec, CageLimits.Default),
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
            var folded = Edit(House, CageHandle.Face(3), new float3(0f, -14f, 0f));
            Assert.That(CageValidator.ValidateSplit(Lot, House, folded, CageLimits.Default),
                Is.Not.EqualTo(CageValidity.Valid));
        }

        [Test]
        public void TryInvert_Blend_RoundTripsRingPoints()
        {
            var blend = SplitDeformations.Create(Lot, House, Tapered()).Blend;
            foreach (var p in new[] { new float3(7f, 0f, 1f), new float3(-7f, 0f, -8f), new float3(8f, 0f, 9f) })
            {
                Assert.That(DeformationInverse.TryInvert(blend, blend.Apply(p, out _), out var back), Is.True);
                Assert.That(math.distance(back, p), Is.LessThan(1e-2f));
            }
        }
    }
}
