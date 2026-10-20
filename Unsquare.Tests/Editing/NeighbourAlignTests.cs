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

        private static NeighbourEdge LotEdge(float ax, float az, float bx, float bz) =>
            new(new float2(ax, az), new float2(bx, bz), false);

        private static bool Find(AlignSide side, params NeighbourEdge[] edges) =>
            NeighbourAlign.TryFindNeighbour(Lot, CageOffsets.Identity, side, edges, Options, out _);

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
            new(new float2(x, -12), new float2(x, 12), false, new float2(outwardX, 0));

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
                out var found), Is.True);
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
            Assert.That(Find(AlignSide.Left, new NeighbourEdge(new float2(5, -12), new float2(5, 12), true)), Is.True);
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
                out var found), Is.True);
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
            var road = new NeighbourEdge(new float2(10, 14), new float2(11, -10), true);
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
    }
}
