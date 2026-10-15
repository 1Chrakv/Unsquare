using NUnit.Framework;
using Unity.Mathematics;
using Unsquare.Core.Cage;
using Unsquare.Core.Editing;

namespace Unsquare.Tests.Editing
{
    public class CagePickerTests
    {
        // 1 cm per metre of distance: ~0.9 m at the 92 m used by rays from above.
        private const float Tolerance = 0.01f;

        private static readonly CageBox Box = new(float3.zero, new float3(10, 8, 20));
        private static readonly float3 Down = new(0, -1, 0);
        private static readonly float3 Back = new(0, 0, -1);

        private static CageHandle? Pick(CageRay ray, CageHandleKind kind, CageOffsets offsets = null) =>
            CagePicker.TryPick(Box, offsets ?? CageOffsets.Identity, ray, Tolerance, kind, out var handle, out _)
                ? handle
                : null;

        [Test]
        public void TryPick_FaceModeFromAbove_PicksTopFace()
        {
            Assert.That(Pick(new CageRay(new float3(5, 100, 10), Down), CageHandleKind.Face),
                Is.EqualTo(CageHandle.Face(3)));
        }

        [Test]
        public void TryPick_NearEdge_FollowsPickKind()
        {
            // Just inside the top front edge (y = 8, z = 20).
            var ray = new CageRay(new float3(5, 100, 19.9f), Down);
            Assert.That(Pick(ray, CageHandleKind.Edge), Is.EqualTo(CageHandle.Edge(3)));
            Assert.That(Pick(ray, CageHandleKind.Face), Is.EqualTo(CageHandle.Face(3)));
        }

        [Test]
        public void TryPick_EdgeModeInsideFace_ReturnsFalse()
        {
            Assert.That(Pick(new CageRay(new float3(5, 100, 10), Down), CageHandleKind.Edge), Is.Null);
        }

        [Test]
        public void TryPick_ThroughTwoFaces_PicksNearest()
        {
            Assert.That(Pick(new CageRay(new float3(5, 4, 100), Back), CageHandleKind.Face),
                Is.EqualTo(CageHandle.Face(5)));
            Assert.That(Pick(new CageRay(new float3(5, 4, -100), -Back), CageHandleKind.Face),
                Is.EqualTo(CageHandle.Face(4)));
        }

        [Test]
        public void TryPick_Miss_ReturnsFalse()
        {
            var ray = new CageRay(new float3(50, 4, 100), Back);
            Assert.That(Pick(ray, CageHandleKind.Face), Is.Null);
            Assert.That(Pick(ray, CageHandleKind.Edge), Is.Null);
            Assert.That(Pick(ray, CageHandleKind.Corner), Is.Null);
        }

        [Test]
        public void TryPickCorner_TwoCornersInLine_PicksNearerOne()
        {
            // Top corner 7 hides bottom corner 5 when looking straight down.
            var ray = new CageRay(new float3(10.2f, 100, 20.1f), Down);
            Assert.That(CagePicker.TryPickCorner(Box, CageOffsets.Identity, ray, Tolerance, out var handle, out _), Is.True);
            Assert.That(handle, Is.EqualTo(CageHandle.Corner(7)));
        }

        [Test]
        public void TryPick_CornerMode_PicksOnlyCorners()
        {
            Assert.That(Pick(new CageRay(new float3(10.2f, 100, 20.1f), Down), CageHandleKind.Corner),
                Is.EqualTo(CageHandle.Corner(7)));
            Assert.That(Pick(new CageRay(new float3(5, 100, 19.9f), Down), CageHandleKind.Corner), Is.Null);
        }

        [Test]
        public void TryPick_DeformedCage_UsesDeformedCorners()
        {
            var raised = CageOffsets.Identity.Move(CageHandle.Face(3), new float3(0, 4, 0));
            var ray = new CageRay(new float3(5, 9.5f, 100), Back);
            Assert.That(Pick(ray, CageHandleKind.Face), Is.Null);
            Assert.That(Pick(ray, CageHandleKind.Face, raised), Is.EqualTo(CageHandle.Face(5)));
        }

        [Test]
        public void TryPickFace_SkewedFace_HitsBothTriangles()
        {
            var skewed = CageOffsets.Identity.Move(CageHandle.Corner(7), new float3(0, 4, 0));
            Assert.That(CagePicker.TryPickFace(Box, skewed, new CageRay(new float3(9, 9, 100), Back), out var a,
                out _),
                Is.True);
            Assert.That(a, Is.EqualTo(CageHandle.Face(5)));
            Assert.That(CagePicker.TryPickFace(Box, skewed, new CageRay(new float3(1, 5, 100), Back), out var b,
                out _),
                Is.True);
            Assert.That(b, Is.EqualTo(CageHandle.Face(5)));
        }
    
        [Test]
        public void TryPickEdge_RayNearVerticalEdge_ReportsGrabbedHeight()
        {
            // Looking along -z at the front left vertical edge (x = 0, z = 20) at 3 m height.
            var ray = new CageRay(new float3(0.1f, 3, 100), Back);
            Assert.That(CagePicker.TryPick(Box, CageOffsets.Identity, ray, Tolerance, CageHandleKind.Edge,
                out var handle, out float along), Is.True);
            Assert.That(CageTopology.EdgeAxis(handle.Index), Is.EqualTo(1));
            Assert.That(ray.PointAt(along).y, Is.EqualTo(3f).Within(1e-4f));
            Assert.That(along, Is.EqualTo(80f).Within(1e-3f));
        }

        [Test]
        public void TryPickFace_Hit_ReportsHitDistance()
        {
            Assert.That(CagePicker.TryPickFace(Box, CageOffsets.Identity, new CageRay(new float3(5, 100, 10), Down),
                out _, out float along), Is.True);
            Assert.That(along, Is.EqualTo(92f).Within(1e-3f));
        }
    }
}
