using System;
using NUnit.Framework;
using Unity.Mathematics;
using Unsquare.Core.Cage;
using Unsquare.Core.Editing;

namespace Unsquare.Tests.Editing
{
    public class GizmoAndViewTests
    {
        [Test]
        public void AxisLength_FarCamera_GrowsAboveMinimum()
        {
            Assert.That(GizmoAxes.AxisLength(10f, 0.1f, 2f), Is.EqualTo(2f));
            Assert.That(GizmoAxes.AxisLength(100f, 0.1f, 2f), Is.EqualTo(10f).Within(1e-4f));
        }

        [Test]
        public void TryPickAxis_NearArrow_PicksIt()
        {
            var down = new float3(0, -1, 0);
            Assert.That(GizmoAxes.TryPickAxis(float3.zero, 2f, new CageRay(new float3(1, 100, 0.05f), down), 0.01f,
                out int x), Is.True);
            Assert.That(x, Is.EqualTo(0));
            Assert.That(GizmoAxes.TryPickAxis(float3.zero, 2f,
                new CageRay(new float3(0, 1, 100), new float3(0, 0, -1)), 0.01f, out int y), Is.True);
            Assert.That(y, Is.EqualTo(1));
        }

        [Test]
        public void TryPickAxis_FarAway_ReturnsFalse()
        {
            var ray = new CageRay(new float3(10, 100, 10), new float3(0, -1, 0));
            Assert.That(GizmoAxes.TryPickAxis(float3.zero, 2f, ray, 0.01f, out _), Is.False);
        }

        [Test]
        public void ViewScale_Fov90_ConvertsPixelsAndFractions()
        {
            var scale = new ViewScale(90f, 1000f);
            Assert.That(scale.ViewHeightPerMeter, Is.EqualTo(2f).Within(1e-5f));
            Assert.That(scale.AngularSizeOfPixels(10f), Is.EqualTo(0.02f).Within(1e-6f));
            Assert.That(scale.AngularSizeOfViewFraction(0.1f), Is.EqualTo(0.2f).Within(1e-6f));
        }

        [Test]
        public void ViewScale_InvalidInput_Throws()
        {
            Assert.Throws<ArgumentException>(() => new ViewScale(0f, 1000f));
            Assert.Throws<ArgumentException>(() => new ViewScale(60f, 0f));
        }

        [Test]
        public void CageRay_ZeroDirection_Throws()
        {
            Assert.Throws<ArgumentException>(() => new CageRay(float3.zero, float3.zero));
        }

        [Test]
        public void DistanceToSegment_ParallelRay_MeasuresGap()
        {
            var ray = new CageRay(new float3(0, 1, 0), new float3(1, 0, 0));
            float distance = RayGeometry.DistanceToSegment(ray, new float3(2, 0, 0), new float3(5, 0, 0), out _);
            Assert.That(distance, Is.EqualTo(1f).Within(1e-5f));
        }

        [Test]
        public void IntersectTriangle_HitBehindOrigin_DoesNotCount()
        {
            var ray = new CageRay(new float3(0.2f, 0.2f, -1), new float3(0, 0, -1));
            Assert.That(RayGeometry.IntersectTriangle(ray, float3.zero, new float3(1, 0, 0), new float3(0, 1, 0),
                out _), Is.False);
        }

        [Test]
        public void CageHover_SameTarget_AreEqual()
        {
            Assert.That(CageHover.OnHandle(CageHandle.Edge(2)), Is.EqualTo(CageHover.OnHandle(CageHandle.Edge(2))));
            Assert.That(CageHover.OnAxis(1).HasAxis, Is.True);
            Assert.That(CageHover.None.HasHandle || CageHover.None.HasAxis, Is.False);
            Assert.That(CageHover.OnAxis(0), Is.Not.EqualTo(CageHover.None));
            Assert.Throws<ArgumentOutOfRangeException>(() => CageHover.OnAxis(3));
        }
    }
}
