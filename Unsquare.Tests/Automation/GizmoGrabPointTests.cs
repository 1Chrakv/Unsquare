using NUnit.Framework;
using Unity.Mathematics;
using Unsquare.Core.Automation;
using Unsquare.Core.Cage;
using Unsquare.Core.Editing;

namespace Unsquare.Tests.Automation
{
    public class GizmoGrabPointTests
    {
        private static readonly float3 Origin = new(1f, 2f, 3f);
        private static readonly float3 Eye = new(50f, 40f, 60f);

        [TestCase("arrowX", 0)]
        [TestCase("ArrowY", 1)]
        [TestCase("arrowz", 2)]
        [TestCase("plane", GizmoAxes.PlaneXZ)]
        [TestCase("ringX", GizmoAxes.RingFirst)]
        [TestCase("RINGZ", GizmoAxes.RingFirst + 2)]
        public void TryParsePart_KnownName_ReturnsPart(string name, int expected)
        {
            Assert.That(GizmoGrabPoint.TryParsePart(name, out int part), Is.True);
            Assert.That(part, Is.EqualTo(expected));
        }

        [TestCase("")]
        [TestCase(null)]
        [TestCase("arrowW")]
        [TestCase("ring")]
        [TestCase("ringXX")]
        public void TryParsePart_UnknownName_ReturnsFalse(string name)
        {
            Assert.That(GizmoGrabPoint.TryParsePart(name, out _), Is.False);
        }

        [Test]
        public void TryGet_Arrow_LiesAlongTheAxisAtTheFraction()
        {
            Assert.That(GizmoGrabPoint.TryGet(CageHandle.Corner(0), 1, Origin, 10f, Eye, out var point), Is.True);
            Assert.That(math.distance(point, Origin + new float3(0f, 7f, 0f)), Is.LessThan(1e-4f));
        }

        [Test]
        public void TryGet_Plane_IsPickedAsThePlane()
        {
            GizmoGrabPoint.TryGet(CageHandle.Face(0), GizmoAxes.PlaneXZ, Origin, 10f, Eye, out var point);
            var ray = new CageRay(point + new float3(0f, 5f, 0f), new float3(0f, -1f, 0f));
            Assert.That(GizmoAxes.TryPickPlane(Origin, 10f, ray), Is.True);
        }

        [Test]
        public void TryGet_RingOfCorner_ReturnsFalse()
        {
            Assert.That(GizmoGrabPoint.TryGet(CageHandle.Corner(0), GizmoAxes.Ring(1), Origin, 10f, Eye, out _),
                Is.False);
        }

        [Test]
        public void TryGet_Ring_LiesOnTheRingFacingTheEye()
        {
            Assert.That(GizmoGrabPoint.TryGet(CageHandle.Face(0), GizmoAxes.Ring(1), Origin, 10f, Eye, out var point),
                Is.True);
            Assert.That(point.y, Is.EqualTo(Origin.y).Within(1e-4f), "on the ring around Y");
            Assert.That(math.distance(point, Origin), Is.EqualTo(10f).Within(1e-3f));
            Assert.That(point.x, Is.GreaterThan(Origin.x), "towards the eye");
            Assert.That(point.z, Is.GreaterThan(Origin.z), "towards the eye");
        }

        [Test]
        public void TryGet_Ring_IsPickedAsThatRing()
        {
            var handle = CageHandle.Face(0);
            GizmoGrabPoint.TryGet(handle, GizmoAxes.Ring(0), Origin, 10f, Eye, out var point);
            var ray = new CageRay(Eye, math.normalize(point - Eye));
            Assert.That(GizmoAxes.TryPickRing(Origin, 10f, handle, ray, 0.01f, out int part), Is.True);
            Assert.That(part, Is.EqualTo(GizmoAxes.Ring(0)));
        }
    }
}
