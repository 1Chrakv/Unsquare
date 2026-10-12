using System;
using NUnit.Framework;
using Unity.Mathematics;
using Unsquare.Core.Cage;
using Unsquare.Core.Terrain;

namespace Unsquare.Tests.Terrain
{
    public class GroundFitTests
    {
        private static readonly CageBox Lot = new(new float3(-8, 0, -12), new float3(8, 15, 12));

        [Test]
        public void Fit_FlatGroundAtZero_KeepsOffsets()
        {
            var offsets = CageOffsets.Identity.Move(CageHandle.Corner(7), new float3(1f, 2f, 0f));
            Assert.That(GroundFit.Fit(Lot, offsets, new[] { 0f, 0f, 0f, 0f }, GroundFitMode.AllCorners), Is.EqualTo(offsets));
        }

        [Test]
        public void Fit_Heights_MoveBottomCornersAndKeepColumnHeights()
        {
            var raisedTop = CageOffsets.Identity.Move(CageHandle.Corner(3), new float3(0f, 2f, 0f));
            var heights = new[] { 1f, 2f, -1.5f, 3.2f };
            var fitted = GroundFit.Fit(Lot, raisedTop, heights, GroundFitMode.AllCorners);
            for (int i = 0; i < GroundFit.GroundCorners.Count; i++)
            {
                int bottom = GroundFit.GroundCorners[i], top = bottom | 2;
                Assert.That(fitted.Meters(bottom).y, Is.EqualTo(heights[i]).Within(1e-4f), $"corner {bottom}");
                float before = raisedTop.Meters(top).y - raisedTop.Meters(bottom).y;
                float after = fitted.Meters(top).y - fitted.Meters(bottom).y;
                Assert.That(after, Is.EqualTo(before).Within(1e-4f), $"column {bottom}");
            }
        }

        [Test]
        public void Fit_Basement_PutsGroundPlaneNotBoxBottomOnTerrain()
        {
            var basement = new CageBox(new float3(-8, -5, -12), new float3(8, 15, 12));
            // Tilting a column moves its ground point by a quarter of the top offset (ground is 5 m up a 20 m column).
            var offsets = CageOffsets.Identity.Move(CageHandle.Corner(2), new float3(0f, 4f, 0f));
            var fitted = GroundFit.Fit(basement, offsets, new[] { 2f, 0f, 0f, 0f }, GroundFitMode.AllCorners);
            Assert.That(fitted.Meters(0).y, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(fitted.Meters(2).y, Is.EqualTo(5f).Within(1e-4f));
        }

        [Test]
        public void Fit_KeepsHorizontalOffsets()
        {
            var offsets = CageOffsets.Identity.Move(CageHandle.Corner(4), new float3(1.5f, 0f, -0.7f));
            var fitted = GroundFit.Fit(Lot, offsets, new[] { 1f, 1f, 1f, 1f }, GroundFitMode.AllCorners);
            for (int corner = 0; corner < CageTopology.CornerCount; corner++)
                Assert.That(fitted.Meters(corner).xz, Is.EqualTo(offsets.Meters(corner).xz), $"corner {corner}");
        }

        [Test]
        public void GroundPoints_FollowSkewedColumns()
        {
            var basement = new CageBox(new float3(-8, -5, -12), new float3(8, 15, 12));
            var offsets = CageOffsets.Identity.Move(CageHandle.Corner(3), new float3(4f, 0f, 0f));
            var points = GroundFit.GroundPoints(basement, offsets);
            Assert.That(points[1].x, Is.EqualTo(9f).Within(1e-4f)); // corner 1 column leans +4 m over 20 m, ground at 5 m
            Assert.That(points[0], Is.EqualTo(new float2(-8f, -12f)));
        }

        [Test]
        public void Fit_BackOnly_MovesBackColumnsToTerrain()
        {
            var fitted = GroundFit.Fit(Lot, CageOffsets.Identity, new[] { -2f, -3f, 5f, 5f }, GroundFitMode.BackOnly);
            Assert.That(fitted.Meters(0).y, Is.EqualTo(-2f).Within(1e-4f));
            Assert.That(fitted.Meters(1).y, Is.EqualTo(-3f).Within(1e-4f));
            Assert.That(fitted.Meters(3).y, Is.EqualTo(-3f).Within(1e-4f)); // top corner moves with its column
        }

        [Test]
        public void Fit_BackOnly_PutsFrontColumnsAtRoadLevel()
        {
            // A raised street-side column comes back to road level and keeps its height.
            var raisedFront = CageOffsets.Identity
                .Move(CageHandle.Corner(4), new float3(0f, 1.5f, 0f))
                .Move(CageHandle.Corner(6), new float3(0f, 2f, 0f));
            var fitted = GroundFit.Fit(Lot, raisedFront, new[] { 0f, 0f, 4f, 4f }, GroundFitMode.BackOnly);
            Assert.That(fitted.Meters(4).y, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(fitted.Meters(6).y, Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(fitted.Meters(5).y, Is.EqualTo(0f).Within(1e-4f));
        }

        [Test]
        public void IsFrontColumn_StreetSideIsPositiveZ()
        {
            Assert.That(GroundFit.IsFrontColumn(4), Is.True);
            Assert.That(GroundFit.IsFrontColumn(5), Is.True);
            Assert.That(GroundFit.IsFrontColumn(0), Is.False);
            Assert.That(GroundFit.IsFrontColumn(1), Is.False);
        }

        [Test]
        public void Fit_WrongHeightCount_Throws()
        {
            Assert.That(() => GroundFit.Fit(Lot, CageOffsets.Identity, new[] { 0f, 0f }, GroundFitMode.AllCorners),
                Throws.InstanceOf<ArgumentException>());
        }
    }
}
