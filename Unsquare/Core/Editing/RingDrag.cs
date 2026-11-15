using System;
using Unity.Mathematics;

namespace Unsquare.Core.Editing
{
    /// <summary>
    /// Maps mouse rays onto a turn around a gizmo ring's axis. A ring seen at an angle follows the ray's hit on its
    /// plane; one seen nearly edge-on is dragged along the tangent at the grabbed spot. The choice is made at the
    /// press and kept, so a drag never jumps.
    /// </summary>
    public sealed class RingDrag : IHandleDrag
    {
        // Below this sine between ray and ring plane the hit runs off; the tangent is used instead.
        private const float PlaneMinSine = 0.3f;

        // Below ~3° between ray and tangent the projection explodes; such frames are skipped.
        private const float LineMinSine = 0.05f;

        // Plane hits this close to the pivot (part of the radius) have no stable angle; such frames are skipped.
        private const float MinRadiusFraction = 0.05f;

        // Candidates on the ring within this angle of each other count as equally close: the nearer one wins.
        private const float TieRadians = 1e-3f;

        private readonly float3 _pivot;
        private readonly float _radius;
        private readonly float _start;
        private readonly float3 _grab;
        private readonly float3 _tangent;

        private RingDrag(int axis, float3 pivot, float radius, bool usesPlane, float start, float3 grab,
            float3 tangent)
        {
            Axis = axis;
            _pivot = pivot;
            _radius = radius;
            UsesPlane = usesPlane;
            _start = start;
            _grab = grab;
            _tangent = tangent;
        }

        public int Axis { get; }

        /// <summary>True if the drag follows the ring plane, false if it slides along the tangent.</summary>
        public bool UsesPlane { get; }

        /// <param name="pivot">Centre of the ring (the handle) at the press, in building space.</param>
        /// <param name="radius">Ring radius in metres.</param>
        public static bool TryStart(float3 pivot, int axis, float radius, CageRay ray, out RingDrag drag)
        {
            if (!(radius > 0f))
                throw new ArgumentException("Ring radius must be positive.", nameof(radius));
            drag = null;
            float3 normal = GizmoAxes.Direction(axis);
            if (math.abs(math.dot(ray.Direction, normal)) >= PlaneMinSine)
            {
                if (!TryPlaneAngle(pivot, axis, radius, ray, out float angle))
                    return false;
                drag = new RingDrag(axis, pivot, radius, true, angle, float3.zero, float3.zero);
                return true;
            }

            if (!TryGrab(pivot, axis, radius, ray, out var grab))
                return false;
            var tangent = math.normalize(math.cross(normal, grab - pivot));
            if (!RayGeometry.TryClosestOnLine(ray, grab, tangent, LineMinSine, out float start))
                return false;
            drag = new RingDrag(axis, pivot, radius, false, start, grab, tangent);
            return true;
        }

        /// <summary>Total turn since the press in radians; false for frames the ring cannot map.</summary>
        public bool TryAngle(CageRay ray, out float angle)
        {
            angle = 0f;
            if (!UsesPlane)
            {
                if (!RayGeometry.TryClosestOnLine(ray, _grab, _tangent, LineMinSine, out float t))
                    return false;
                angle = (t - _start) / _radius;
                return true;
            }
            if (!TryPlaneAngle(_pivot, Axis, _radius, ray, out float now))
                return false;
            float delta = now - _start;
            angle = delta - 2f * math.PI * math.round(delta / (2f * math.PI));
            return true;
        }

        public bool TryMotion(CageRay ray, out HandleMotion motion)
        {
            bool ok = TryAngle(ray, out float angle);
            motion = HandleMotion.Rotate(Axis, angle);
            return ok;
        }

        // Angle of the ray's hit on the ring plane, counter-clockwise around the axis.
        private static bool TryPlaneAngle(float3 pivot, int axis, float radius, CageRay ray, out float angle)
        {
            angle = 0f;
            if (!RayGeometry.IntersectPlane(ray, pivot, GizmoAxes.Direction(axis), out float along))
                return false;
            float3 d = ray.PointAt(along) - pivot;
            float u = math.dot(d, GizmoAxes.Direction((axis + 1) % 3));
            float v = math.dot(d, GizmoAxes.Direction((axis + 2) % 3));
            if (u * u + v * v < radius * radius * MinRadiusFraction * MinRadiusFraction)
                return false;
            angle = math.atan2(v, u);
            return true;
        }

        // The spot on the ring under the mouse; of two equally close (front and back of an edge-on ring) the nearer.
        private static bool TryGrab(float3 pivot, int axis, float radius, CageRay ray, out float3 grab)
        {
            grab = float3.zero;
            float bestAngle = float.MaxValue;
            float bestAlong = float.MaxValue;
            float3 previous = GizmoAxes.RingPoint(pivot, radius, axis, 0);
            for (int i = 1; i <= GizmoAxes.RingSegments; i++)
            {
                float3 next = GizmoAxes.RingPoint(pivot, radius, axis, i);
                float distance = RayGeometry.DistanceToSegment(ray, previous, next, out float along);
                previous = next;
                if (!(along > 0f))
                    continue;
                float angle = distance / along;
                if (angle > bestAngle + TieRadians || (angle > bestAngle - TieRadians && along >= bestAlong))
                    continue;
                bestAngle = angle;
                bestAlong = along;
            }
            if (bestAlong == float.MaxValue)
                return false;
            float3 normal = GizmoAxes.Direction(axis);
            float3 radial = ray.PointAt(bestAlong) - pivot;
            radial -= normal * math.dot(radial, normal);
            if (math.lengthsq(radial) < 1e-8f)
                return false;
            grab = pivot + math.normalize(radial) * radius;
            return true;
        }
    }
}
