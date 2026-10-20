using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using Unsquare.Core.Cage;
using Unsquare.Core.Editing;

namespace Unsquare.Tests.Editing
{
    public class NeighbourAlignTests
    {
        // Left face (+X) runs from corner 1 (8, -12) to corner 5 (8, 12); the back face (−Z) from 0 to 1.
        private static readonly CageBox Lot = new(new float3(-8, 0, -12), new float3(8, 15, 12));
        private static readonly NeighbourAlignOptions Options = NeighbourAlignOptions.Default;

        // A neighbour lot's side facing this lot (its outward normal points towards the lot centre).
        private static NeighbourEdge LotEdge(float ax, float az, float bx, float bz, float height = 0f)
        {
            float2 a = new(ax, az), b = new(bx, bz);
            float2 normal = math.normalize(new float2(b.y - a.y, a.x - b.x));
            return NeighbourEdge.LotSide(Ground(a, height), Ground(b, height),
                math.dot(normal, -(a + b) * 0.5f) < 0f ? -normal : normal);
        }

        private static float3 Ground(float2 xz, float height = 0f) => new(xz.x, height, xz.y);

        private static bool Find(AlignSide side, params NeighbourEdge[] edges) =>
            NeighbourAlign.TryFindNeighbour(Lot, CageOffsets.Identity, side, edges, Options, out _, out _);

        private static void AssertXz(CageOffsets offsets, int corner, float x, float z)
        {
            Assert.That(offsets.Meters(corner).x, Is.EqualTo(x).Within(1e-4f), $"corner {corner} x");
            Assert.That(offsets.Meters(corner).z, Is.EqualTo(z).Within(1e-4f), $"corner {corner} z");
        }

        [Test]
        public void TryFindNeighbour_ParallelEdgeOutsideFace_IsFound()
        {
            Assert.That(Find(AlignSide.Left, LotEdge(10, -12, 10, 12)), Is.True);
            Assert.That(Find(AlignSide.Right, LotEdge(-10, -12, -10, 12)), Is.True);
            Assert.That(Find(AlignSide.Back, LotEdge(-8, -14, 8, -14)), Is.True);
        }

        [Test]
        public void TryFindNeighbour_EdgeOnOtherSide_IsIgnored()
        {
            Assert.That(Find(AlignSide.Left, LotEdge(-10, -12, -10, 12)), Is.False);
        }

        [Test]
        public void TryFindNeighbour_TooFarTooTurnedOrBeside_IsIgnored()
        {
            Assert.That(Find(AlignSide.Left, LotEdge(260, -12, 260, 12)), Is.False);
            Assert.That(Find(AlignSide.Left, LotEdge(10, 0, 20, 0)), Is.False);
            Assert.That(Find(AlignSide.Left, LotEdge(10, 20, 10, 30)), Is.False);
        }

        private static NeighbourEdge FacingX(float x, float outwardX) =>
            NeighbourEdge.LotSide(new float3(x, 0, -12), new float3(x, 0, 12), new float2(outwardX, 0));

        [Test]
        public void TryFindNeighbour_ClippedIntoNeighbour_FindsItsFacingEdge()
        {
            Assert.That(Find(AlignSide.Left, FacingX(5, -1)), Is.True);
            var aligned = NeighbourAlign.Align(Lot, CageOffsets.Identity, AlignSide.Left, FacingX(5, -1),
                AlignMode.Flush);
            AssertXz(aligned, 1, -3f, 0f);
            AssertXz(aligned, 5, -3f, 0f);
        }

        [Test]
        public void TryFindNeighbour_DeepClip_PrefersFacingEdgeOverNearerFarSide()
        {
            var facing = FacingX(2, -1);
            var edges = new[] { FacingX(10, 1), facing };
            Assert.That(NeighbourAlign.TryFindNeighbour(Lot, CageOffsets.Identity, AlignSide.Left, edges, Options,
                out var found, out _), Is.True);
            Assert.That(found.A, Is.EqualTo(facing.A));
        }

        [Test]
        public void TryFindNeighbour_EdgeFacingAway_IsIgnored()
        {
            Assert.That(Find(AlignSide.Left, FacingX(10, 1)), Is.False);
        }

        [Test]
        public void TryFindNeighbour_DeeperThanHalfTheLot_IsIgnored()
        {
            Assert.That(Find(AlignSide.Left, LotEdge(-1, -12, -1, 12)), Is.False);
        }

        [Test]
        public void TryFindNeighbour_RoadInsideLot_IsFound()
        {
            Assert.That(Find(AlignSide.Left, NeighbourEdge.Road(new float3(5, 0, -12), new float3(5, 0, 12))), Is.True);
        }

        [Test]
        public void TryFindNeighbour_ThreeCellsAway_IsWithinDefaultReach()
        {
            Assert.That(Find(AlignSide.Left, LotEdge(32, -12, 32, 12)), Is.True);
        }

        [Test]
        public void TryFindNeighbour_OutOfReach_ReportsNearestDistance()
        {
            var edges = new[] { LotEdge(40, -12, 40, 12), LotEdge(32, -12, 32, 12) };
            bool found = NeighbourAlign.TryFindNeighbour(Lot, CageOffsets.Identity, AlignSide.Left, edges,
                new NeighbourAlignOptions(16f, 45f), out _, out float nearest);
            Assert.That(found, Is.False);
            Assert.That(nearest, Is.EqualTo(24f).Within(1e-4f));
        }

        [Test]
        public void TryFindNeighbour_NothingParallel_ReportsInfinity()
        {
            NeighbourAlign.TryFindNeighbour(Lot, CageOffsets.Identity, AlignSide.Left, new[] { LotEdge(10, 0, 20, 0) },
                Options, out _, out float nearest);
            Assert.That(float.IsPositiveInfinity(nearest), Is.True);
        }

        [Test]
        public void TryFindNeighbour_SeveralCandidates_PicksNearest()
        {
            var near = LotEdge(10, -12, 10, 12);
            var edges = new[] { LotEdge(14, -12, 14, 12), near, LotEdge(-10, -12, -10, 12) };
            Assert.That(NeighbourAlign.TryFindNeighbour(Lot, CageOffsets.Identity, AlignSide.Left, edges, Options,
                out var found, out _), Is.True);
            Assert.That(found.A, Is.EqualTo(near.A));
        }

        [Test]
        public void TryFindNeighbour_SlightOverlap_IsFound()
        {
            Assert.That(Find(AlignSide.Left, LotEdge(7.5f, -12, 7.5f, 12)), Is.True);
        }

        [Test]
        public void Align_Angle_KeepsNearerCornerAndSlidesOtherAlongItsEdge()
        {
            // Line through (10, -12) and (12, 12): corner 1 is nearer, corner 5 slides along z = 12 to x = 10.
            var aligned = NeighbourAlign.Align(Lot, CageOffsets.Identity, AlignSide.Left, LotEdge(10, -12, 12, 12),
                AlignMode.Angle);
            AssertXz(aligned, 1, 0f, 0f);
            AssertXz(aligned, 5, 2f, 0f);
            AssertXz(aligned, 7, 2f, 0f);
        }

        [Test]
        public void Align_Flush_PutsBothCornersOnNeighbourLine()
        {
            var aligned = NeighbourAlign.Align(Lot, CageOffsets.Identity, AlignSide.Left, LotEdge(10, -12, 12, 12),
                AlignMode.Flush);
            AssertXz(aligned, 1, 2f, 0f);
            AssertXz(aligned, 3, 2f, 0f);
            AssertXz(aligned, 5, 4f, 0f);
            AssertXz(aligned, 7, 4f, 0f);
            AssertXz(aligned, 0, 0f, 0f);
        }

        [Test]
        public void Align_FullOnLot_TakesNeighbourEndPointsOnMatchingSides()
        {
            var aligned = NeighbourAlign.Align(Lot, CageOffsets.Identity, AlignSide.Left, LotEdge(10, 14, 11, -10),
                AlignMode.Full);
            AssertXz(aligned, 1, 3f, 2f);
            AssertXz(aligned, 5, 2f, 2f);
        }

        [Test]
        public void Align_FullOnRoad_IsFlush()
        {
            var road = NeighbourEdge.Road(new float3(10, 0, 14), new float3(11, 0, -10));
            var full = NeighbourAlign.Align(Lot, CageOffsets.Identity, AlignSide.Left, road, AlignMode.Full);
            var flush = NeighbourAlign.Align(Lot, CageOffsets.Identity, AlignSide.Left, road, AlignMode.Flush);
            Assert.That(full, Is.EqualTo(flush));
        }

        [Test]
        public void Align_Back_MovesBackCornersAlongSides()
        {
            var aligned = NeighbourAlign.Align(Lot, CageOffsets.Identity, AlignSide.Back, LotEdge(-20, -15, 20, -15),
                AlignMode.Flush);
            AssertXz(aligned, 0, 0f, -3f);
            AssertXz(aligned, 1, 0f, -3f);
            AssertXz(aligned, 4, 0f, 0f);
        }

        [Test]
        public void Align_KeepsHeights()
        {
            var raised = CageOffsets.Identity.Move(CageHandle.Corner(5), new float3(0f, 1.5f, 0f))
                .Move(CageHandle.Corner(7), new float3(0f, 2f, 0f));
            var aligned = NeighbourAlign.Align(Lot, raised, AlignSide.Left, LotEdge(10, -12, 12, 12), AlignMode.Flush);
            for (int corner = 0; corner < CageTopology.CornerCount; corner++)
                Assert.That(aligned.Meters(corner).y, Is.EqualTo(raised.Meters(corner).y), $"corner {corner}");
        }

        [Test]
        public void Align_AlreadyAligned_ReturnsSameOffsets()
        {
            var aligned = NeighbourAlign.Align(Lot, CageOffsets.Identity, AlignSide.Left, LotEdge(8, -20, 8, 20),
                AlignMode.Flush);
            Assert.That(aligned.IsIdentity, Is.True);
        }

        [Test]
        public void Options_InvalidValues_Throw()
        {
            Assert.That(() => new NeighbourAlignOptions(0f, 30f), Throws.ArgumentException);
            Assert.That(() => new NeighbourAlignOptions(10f, 90f), Throws.ArgumentException);
        }

        [Test]
        public void AddLotOutline_SidesFaceAwayFromTheLot()
        {
            // Ground points in GroundFit order: corners 0 (-x,-z), 1 (+x,-z), 4 (-x,+z), 5 (+x,+z).
            var points = new[] { new float3(-2, 1, -3), new float3(2, 1, -3), new float3(-2, 1, 3), new float3(2, 1, 3) };
            var edges = new List<NeighbourEdge>();
            NeighbourEdge.AddLotOutline(points, edges);
            Assert.That(edges, Has.Count.EqualTo(4));
            foreach (var edge in edges)
            {
                float2 mid = (edge.A + edge.B) * 0.5f;
                Assert.That(math.dot(edge.Outward, mid), Is.GreaterThan(0f), edge.ToString());
                Assert.That(math.length(edge.Outward), Is.EqualTo(1f).Within(1e-5f));
                Assert.That(edge.IsRoad, Is.False);
                Assert.That(edge.HeightA, Is.EqualTo(1f));
            }
        }

        [Test]
        public void FitHeight_HigherLot_RaisesOnlyThatSidesColumns()
        {
            var raisedTop = CageOffsets.Identity.Move(CageHandle.Corner(7), new float3(0f, 2f, 0f));
            var fitted = NeighbourAlign.FitHeight(Lot, raisedTop, AlignSide.Left, LotEdge(10, -12, 10, 12, 2f));
            foreach (int bottom in new[] { 1, 5 })
            {
                Assert.That(fitted.Meters(bottom).y, Is.EqualTo(2f).Within(1e-4f), $"corner {bottom}");
                float before = raisedTop.Meters(bottom | 2).y - raisedTop.Meters(bottom).y;
                float after = fitted.Meters(bottom | 2).y - fitted.Meters(bottom).y;
                Assert.That(after, Is.EqualTo(before).Within(1e-4f), $"column {bottom}");
            }
            foreach (int corner in new[] { 0, 2, 4, 6 })
                Assert.That(fitted.Meters(corner), Is.EqualTo(raisedTop.Meters(corner)), $"corner {corner}");
        }

        [Test]
        public void FitHeight_SlopedRoad_InterpolatesPerColumn()
        {
            // Road edge from (10, z -20, 0 m) to (10, z 20, 4 m): corner 1 (z -12) at 0.8 m, corner 5 (z 12) at 3.2 m.
            var road = NeighbourEdge.Road(new float3(10, 0, -20), new float3(10, 4, 20));
            var fitted = NeighbourAlign.FitHeight(Lot, CageOffsets.Identity, AlignSide.Left, road);
            Assert.That(fitted.Meters(1).y, Is.EqualTo(0.8f).Within(1e-4f));
            Assert.That(fitted.Meters(5).y, Is.EqualTo(3.2f).Within(1e-4f));
        }

        [Test]
        public void FitHeight_BeyondEdgeEnd_UsesNearestEnd()
        {
            var shortEdge = NeighbourEdge.Road(new float3(10, 1, -12), new float3(10, 3, 0));
            var fitted = NeighbourAlign.FitHeight(Lot, CageOffsets.Identity, AlignSide.Left, shortEdge);
            Assert.That(fitted.Meters(1).y, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(fitted.Meters(5).y, Is.EqualTo(3f).Within(1e-4f));
        }

        [Test]
        public void FitHeight_Back_MovesBackColumns()
        {
            var fitted = NeighbourAlign.FitHeight(Lot, CageOffsets.Identity, AlignSide.Back,
                LotEdge(-20, -14, 20, -14, -1.5f));
            Assert.That(fitted.Meters(0).y, Is.EqualTo(-1.5f).Within(1e-4f));
            Assert.That(fitted.Meters(1).y, Is.EqualTo(-1.5f).Within(1e-4f));
            Assert.That(fitted.Meters(4).y, Is.EqualTo(0f));
        }
    }
}
