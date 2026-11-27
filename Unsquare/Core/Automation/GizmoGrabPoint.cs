using System;
using Unity.Mathematics;
using Unsquare.Core.Cage;
using Unsquare.Core.Editing;

namespace Unsquare.Core.Automation
{
    /// <summary>
    /// Where a test presses the mouse to grab a gizmo part, in building space: well inside the part's pick area and
    /// away from where parts overlap (ring crossings, the arrow roots near the square).
    /// </summary>
    public static class GizmoGrabPoint
    {
        /// <summary>Arrows are grabbed this far along their length: clear of the origin and the square.</summary>
        public const float ArrowFraction = 0.7f;

        // Ring points at 45°, 135°, 225° and 315°: the rings cross each other at multiples of 90°.
        private static readonly int[] RingCandidates =
            { GizmoAxes.RingSegments / 8, 3 * GizmoAxes.RingSegments / 8, 5 * GizmoAxes.RingSegments / 8, 7 * GizmoAxes.RingSegments / 8 };

        /// <summary>Gizmo part id for a name: arrowX/Y/Z, plane, ringX/Y/Z (any case).</summary>
        public static bool TryParsePart(string name, out int part)
        {
            part = -1;
            if (string.IsNullOrEmpty(name))
                return false;
            var lower = name.ToLowerInvariant();
            if (lower == "plane")
            {
                part = GizmoAxes.PlaneXZ;
                return true;
            }
            char last = lower[lower.Length - 1];
            if (last < 'x' || last > 'z')
                return false;
            int axis = last - 'x';
            var prefix = lower.Substring(0, lower.Length - 1);
            if (prefix == "arrow")
                part = axis;
            else if (prefix == "ring")
                part = GizmoAxes.Ring(axis);
            return part >= 0;
        }

        /// <summary>
        /// The point to grab <paramref name="part"/> of the gizmo at <paramref name="origin"/>; rings take the
        /// candidate nearest the eye so it lies in front. False for a ring the handle has none of.
        /// </summary>
        public static bool TryGet(CageHandle handle, int part, float3 origin, float length, float3 eye, out float3 point)
        {
            point = default;
            if (part >= 0 && part < GizmoAxes.AxisCount)
            {
                point = origin + GizmoAxes.Direction(part) * (ArrowFraction * length);
                return true;
            }
            if (part == GizmoAxes.PlaneXZ)
            {
                float middle = (GizmoAxes.PlaneInner + GizmoAxes.PlaneOuter) / 2f;
                point = origin + new float3(middle, 0f, middle) * length;
                return true;
            }
            if (!GizmoAxes.IsRing(part))
                throw new ArgumentOutOfRangeException(nameof(part), part, null);
            int axis = GizmoAxes.RingAxis(part);
            if (!GizmoAxes.CanRotate(handle, axis))
                return false;
            float best = float.MaxValue;
            foreach (int index in RingCandidates)
            {
                float3 candidate = GizmoAxes.RingPoint(origin, length, axis, index);
                float distance = math.distancesq(candidate, eye);
                if (distance < best)
                {
                    best = distance;
                    point = candidate;
                }
            }
            return true;
        }
    }
}
