using System;
using NUnit.Framework;
using Unity.Mathematics;
using Unsquare.Core.Cage;
using Unsquare.Core.Editing;

namespace Unsquare.Tests.Editing
{
    public class RotationGizmoTests
    {
        private const int Y = 1;
        private static readonly CageBox Box = new(new float3(-5, 0, -5), new float3(5, 10, 5));
        private static readonly CageHandle Top = CageHandle.Face(3);

        [Test]
        public void CanRotate_PerHandleKind_OffersNonTrivialAxes()
        {
            for (int axis = 0; axis < GizmoAxes.AxisCount; axis++)
            {
                Assert.That(GizmoAxes.CanRotate(CageHandle.Corner(5), axis), Is.False);
                Assert.That(GizmoAxes.CanRotate(Top, axis), Is.True);
                Assert.That(GizmoAxes.CanRotate(CageHandle.Face(0), axis), Is.True);
            }
            // Edges 0..3 run along X, 4..7 along Y (vertical), 8..11 along Z.
            Assert.That(GizmoAxes.CanRotate(CageHandle.Edge(0), 0), Is.False);
            Assert.That(GizmoAxes.CanRotate(CageHandle.Edge(0), 1), Is.True);
            Assert.That(GizmoAxes.CanRotate(CageHandle.Edge(0), 2), Is.True);
            Assert.That(GizmoAxes.CanRotate(CageHandle.Edge(5), 1), Is.False);
            Assert.That(GizmoAxes.CanRotate(CageHandle.Edge(5), 0), Is.True);
            Assert.That(GizmoAxes.CanRotate(CageHandle.Edge(9), 2), Is.False);
            Assert.That(GizmoAxes.HasRings(CageHandle.Corner(0)), Is.False);
            Assert.That(GizmoAxes.HasRings(CageHandle.Edge(5)), Is.True);
        }

        [Test]
        public void Ring_PartIds_RoundTrip()
        {
            for (int axis = 0; axis < GizmoAxes.AxisCount; axis++)
            {
                int part = GizmoAxes.Ring(axis);
                Assert.That(GizmoAxes.IsRing(part), Is.True);
                Assert.That(GizmoAxes.RingAxis(part), Is.EqualTo(axis));
            }
            Assert.That(GizmoAxes.IsRing(GizmoAxes.PlaneXZ), Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() => GizmoAxes.Ring(3));
            Assert.Throws<ArgumentOutOfRangeException>(() => GizmoAxes.RingAxis(0));
        }

        [Test]
        public void RingPoint_TurnsCounterClockwiseAroundAxis()
        {
            float3 a = GizmoAxes.RingPoint(float3.zero, 2f, Y, 0);
            float3 b = GizmoAxes.RingPoint(float3.zero, 2f, Y, 1);
            Assert.That(math.length(a), Is.EqualTo(2f).Within(1e-5f));
            Assert.That(math.dot(math.cross(a, b), GizmoAxes.Direction(Y)), Is.GreaterThan(0f));
        }

        [Test]
        public void TryPickRing_RayOnYRing_PicksIt()
        {
            float r = math.sqrt(2f);
            var ray = new CageRay(new float3(r, 100, r), new float3(0, -1, 0));
            Assert.That(GizmoAxes.TryPickRing(float3.zero, 2f, Top, ray, 0.01f, out int part), Is.True);
            Assert.That(part, Is.EqualTo(GizmoAxes.Ring(Y)));
        }

        [Test]
        public void TryPickRing_RingTheHandleLacks_Misses()
        {
            // A vertical edge has no Y ring.
            float r = math.sqrt(2f);
            var ray = new CageRay(new float3(r, 100, r), new float3(0, -1, 0));
            Assert.That(GizmoAxes.TryPickRing(float3.zero, 2f, CageHandle.Edge(5), ray, 0.01f, out _), Is.False);
        }

        [Test]
        public void Apply_ZeroAngle_KeepsOffsets()
        {
            var start = CageOffsets.Identity.Move(Top, new float3(1.3f, 0.5f, -0.2f));
            Assert.That(HandleMotion.Rotate(Y, 0f).Apply(Box, start, Top), Is.EqualTo(start));
        }

        [Test]
        public void Apply_TopTwistedQuarterTurn_MovesCornersOntoEachOther()
        {
            var offsets = HandleMotion.Rotate(Y, math.PI / 2f).Apply(Box, CageOffsets.Identity, Top);
            // Counter-clockwise seen from above: (x, z) -> (z, -x).
            AssertAt(offsets, 2, Box.Corner(6));
            AssertAt(offsets, 6, Box.Corner(7));
            AssertAt(offsets, 7, Box.Corner(3));
            AssertAt(offsets, 3, Box.Corner(2));
            for (int bottom = 0; bottom < 8; bottom += 1)
            {
                if ((bottom & 2) == 0)
                    Assert.That(offsets.Steps(bottom), Is.EqualTo(int3.zero));
            }
        }

        [Test]
        public void Apply_Rotation_KeepsHandleCentre()
        {
            var start = CageOffsets.Identity.Move(Top, new float3(1f, 2f, 0f));
            var offsets = HandleMotion.Rotate(0, 0.3f).Apply(Box, start, Top);
            float3 before = DeformedCage.HandlePosition(Box, start, Top);
            float3 after = DeformedCage.HandlePosition(Box, offsets, Top);
            Assert.That(math.distance(before, after), Is.LessThan(CageOffsets.StepMeters));
            Assert.That(offsets, Is.Not.EqualTo(start));
        }

        [Test]
        public void Apply_Translation_EqualsMove()
        {
            var delta = new float3(0.4f, -1.2f, 2f);
            var edge = CageHandle.Edge(3);
            Assert.That(HandleMotion.Translate(delta).Apply(Box, CageOffsets.Identity, edge),
                Is.EqualTo(CageOffsets.Identity.Move(edge, delta)));
        }

        [Test]
        public void Move_CageHandle_ShiftsEveryCorner()
        {
            var offsets = HandleMotion.Translate(new float3(3f, 0f, -1.5f)).Apply(Box, CageOffsets.Identity,
                CageHandle.Cage());
            for (int corner = 0; corner < CageTopology.CornerCount; corner++)
                Assert.That(offsets.Steps(corner), Is.EqualTo(new int3(30, 0, -15)));
        }

        [Test]
        public void Apply_CageQuarterTurnAroundY_KeepsCentreAndSwapsCorners()
        {
            var cage = CageHandle.Cage();
            var offsets = HandleMotion.Rotate(Y, math.PI / 2f).Apply(Box, CageOffsets.Identity, cage);
            Assert.That(math.distance(DeformedCage.HandlePosition(Box, offsets, cage), Box.Center), Is.LessThan(1e-4f));
            AssertAt(offsets, 0, Box.Corner(4)); // bottom (-x, -z) -> (-x, +z)
            AssertAt(offsets, 2, Box.Corner(6)); // top likewise
            AssertAt(offsets, 5, Box.Corner(1));
            Assert.That(GizmoAxes.HasRings(cage), Is.True);
            for (int axis = 0; axis < GizmoAxes.AxisCount; axis++)
                Assert.That(GizmoAxes.CanRotate(cage, axis), Is.True);
        }

        [Test]
        public void TryAdvance_RotationWithinOneGridStep_ReportsNoChange()
        {
            var drag = new CageDrag(Top, Box, CageOffsets.Identity);
            Assert.That(drag.TryAdvance(HandleMotion.Rotate(Y, 0.001f), out _), Is.False);
            Assert.That(drag.TryAdvance(HandleMotion.Rotate(Y, 0.05f), out var turned), Is.True);
            Assert.That(turned, Is.Not.EqualTo(CageOffsets.Identity));
        }

        [Test]
        public void TryAngle_RayThroughTurnedSpot_FollowsMouse()
        {
            var camera = new float3(3, 40, 15);
            Assert.That(RingDrag.TryStart(float3.zero, Y, 2f, RayTo(camera, RingSpot(0.3f)), out var drag), Is.True);
            Assert.That(drag.UsesPlane, Is.True);
            Assert.That(drag.TryAngle(RayTo(camera, RingSpot(0.8f)), out float angle), Is.True);
            Assert.That(angle, Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(drag.TryAngle(RayTo(camera, RingSpot(0.3f - 1f)), out float back), Is.True);
            Assert.That(back, Is.EqualTo(-1f).Within(1e-4f));
        }

        [Test]
        public void TryAngle_AcrossHalfTurn_StaysWrapped()
        {
            var camera = new float3(0, 40, 0.5f);
            Assert.That(RingDrag.TryStart(float3.zero, Y, 2f, RayTo(camera, RingSpot(3f)), out var drag), Is.True);
            Assert.That(drag.TryAngle(RayTo(camera, RingSpot(3.4f)), out float angle), Is.True);
            Assert.That(angle, Is.EqualTo(0.4f).Within(1e-4f));
        }

        [Test]
        public void TryAngle_EdgeOnRing_SlidesAlongTangent()
        {
            // The Z ring seen from straight above is a line; the spot nearer to the camera, (1, √3, 0), is grabbed.
            var down = new float3(0, -1, 0);
            Assert.That(RingDrag.TryStart(float3.zero, 2, 2f, new CageRay(new float3(1, 100, 0), down),
                out var drag), Is.True);
            Assert.That(drag.UsesPlane, Is.False);
            Assert.That(drag.TryAngle(new CageRay(new float3(1.2f, 100, 0), down), out float angle), Is.True);
            Assert.That(angle, Is.EqualTo(-0.2f / math.sqrt(3f)).Within(0.01f));
        }

        [Test]
        public void TryStart_RingBehindCamera_Fails()
        {
            var ray = new CageRay(new float3(1, 50, 1), new float3(0, 1, 0));
            Assert.That(RingDrag.TryStart(float3.zero, Y, 2f, ray, out _), Is.False);
        }

        [Test]
        public void TryMotion_ReturnsRotationAroundRingAxis()
        {
            var camera = new float3(3, 40, 15);
            RingDrag.TryStart(float3.zero, Y, 2f, RayTo(camera, RingSpot(0f)), out var drag);
            Assert.That(drag.TryMotion(RayTo(camera, RingSpot(0.2f)), out var motion), Is.True);
            Assert.That(motion.IsRotation, Is.True);
            Assert.That(motion.Axis, Is.EqualTo(Y));
            Assert.That(motion.Angle, Is.EqualTo(0.2f).Within(1e-4f));
        }

        [Test]
        public void ModifierTap_PressAndRelease_IsATap()
        {
            var tap = new ModifierTap();
            Assert.That(tap.Update(true, false, 1f), Is.False);
            Assert.That(tap.Update(true, false, 1.1f), Is.False);
            Assert.That(tap.Update(false, false, 1.2f), Is.True);
            Assert.That(tap.Update(false, false, 1.3f), Is.False);
            Assert.That(tap.Update(true, false, 2f), Is.False);
            Assert.That(tap.Update(false, false, 2.1f), Is.True);
        }

        [Test]
        public void ModifierTap_HeldTooLong_IsNoTap()
        {
            var tap = new ModifierTap(0.5f);
            tap.Update(true, false, 1f);
            Assert.That(tap.Update(false, false, 1.6f), Is.False);
        }

        [Test]
        public void ModifierTap_WithOtherKey_IsNoTap()
        {
            var tap = new ModifierTap();
            tap.Update(true, false, 1f);
            tap.Update(true, true, 1.05f); // e.g. Ctrl+Z
            tap.Update(true, false, 1.1f);
            Assert.That(tap.Update(false, false, 1.2f), Is.False);
            tap.Update(true, false, 2f);
            Assert.That(tap.Update(false, false, 2.1f), Is.True);
        }

        private static void AssertAt(CageOffsets offsets, int corner, float3 expected) =>
            Assert.That(math.distance(DeformedCage.CornerPosition(Box, offsets, corner), expected),
                Is.LessThan(1e-4f), $"corner {corner}");

        private static float3 RingSpot(float angle) =>
            new(2f * math.sin(angle), 0f, 2f * math.cos(angle));

        private static CageRay RayTo(float3 camera, float3 target) => new(camera, target - camera);
    }
}
