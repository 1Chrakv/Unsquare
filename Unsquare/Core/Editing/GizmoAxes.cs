using System;
using Unity.Mathematics;
using Unsquare.Core.Cage;

namespace Unsquare.Core.Editing
{
    /// <summary>
    /// The gizmo on the selected handle. Move mode: three arrows along the building axes X, Y, Z (0, 1, 2) and a
    /// square between the X and Z arrows (<see cref="PlaneXZ"/>). Rotate mode: a ring around each axis the handle
    /// can turn around (<see cref="Ring"/>). Shared by picking and drawing.
    /// </summary>
    public static class GizmoAxes
    {
        public const int AxisCount = 3;

        /// <summary>Gizmo part id of the horizontal plane square, after the three axes.</summary>
        public const int PlaneXZ = 3;

        /// <summary>Part id of the ring around X; Y and Z follow.</summary>
        public const int RingFirst = 4;

        /// <summary>Number of gizmo part ids: arrows, square and rings.</summary>
        public const int PartCount = RingFirst + AxisCount;

        /// <summary>Straight pieces a ring is picked and drawn with.</summary>
        public const int RingSegments = 32;

        /// <summary>Square extent along +X and +Z, as fractions of the arrow length.</summary>
        public const float PlaneInner = 0.25f;
        public const float PlaneOuter = 0.55f;

        public static float3 Direction(int axis) => axis switch
        {
            0 => new float3(1f, 0f, 0f),
            1 => new float3(0f, 1f, 0f),
            2 => new float3(0f, 0f, 1f),
            _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, null),
        };

        /// <summary>Part id of the ring around an axis (0..2).</summary>
        public static int Ring(int axis)
        {
            if (axis < 0 || axis >= AxisCount)
                throw new ArgumentOutOfRangeException(nameof(axis), axis, null);
            return RingFirst + axis;
        }

        public static bool IsRing(int part) => part >= RingFirst && part < PartCount;

        /// <summary>The axis a ring part turns around.</summary>
        public static int RingAxis(int part) =>
            IsRing(part) ? part - RingFirst : throw new ArgumentOutOfRangeException(nameof(part), part, null);

        /// <summary>
        /// Whether turning the handle around the axis changes it: never for a corner (a point), not around an edge's
        /// own direction; faces turn around all three.
        /// </summary>
        public static bool CanRotate(CageHandle handle, int axis) => handle.Kind switch
        {
            CageHandleKind.Corner => false,
            CageHandleKind.Edge => axis != CageTopology.EdgeAxis(handle.Index),
            _ => true,
        };

        public static bool HasRings(CageHandle handle) => handle.Kind != CageHandleKind.Corner;

        /// <summary>Screen-constant arrow length, but never shorter than <paramref name="minLength"/> metres.</summary>
        public static float AxisLength(float cameraDistance, float angularSize, float minLength) =>
            math.max(minLength, cameraDistance * angularSize);

        /// <summary>The arrow closest to the ray within the angular tolerance.</summary>
        public static bool TryPickAxis(float3 origin, float length, CageRay ray, float tolerance, out int axis)
        {
            var pick = new ClosestPick(tolerance);
            for (int i = 0; i < AxisCount; i++)
            {
                float distance = RayGeometry.DistanceToSegment(ray, origin, origin + Direction(i) * length,
                    out float along);
                pick.Offer(i, distance, along);
            }
            axis = pick.Index;
            return pick.Found;
        }

        /// <summary>Corner 0..3 of the plane square, walking around it.</summary>
        public static float3 PlaneCorner(float3 origin, float length, int corner)
        {
            if (corner < 0 || corner > 3)
                throw new ArgumentOutOfRangeException(nameof(corner), corner, null);
            float x = corner == 1 || corner == 2 ? PlaneOuter : PlaneInner;
            float z = corner >= 2 ? PlaneOuter : PlaneInner;
            return origin + new float3(x, 0f, z) * length;
        }

        /// <summary>True if the ray crosses the plane square in front of the camera.</summary>
        public static bool TryPickPlane(float3 origin, float length, CageRay ray)
        {
            if (!RayGeometry.IntersectHorizontalPlane(ray, origin.y, out float along))
                return false;
            float3 local = (ray.PointAt(along) - origin) / length;
            return local.x >= PlaneInner && local.x <= PlaneOuter && local.z >= PlaneInner && local.z <= PlaneOuter;
        }

        /// <summary>Point <paramref name="index"/> of <see cref="RingSegments"/>, counter-clockwise around the axis.</summary>
        public static float3 RingPoint(float3 origin, float radius, int axis, int index)
        {
            math.sincos(index * (2f * math.PI / RingSegments), out float sin, out float cos);
            return origin + (Direction((axis + 1) % 3) * cos + Direction((axis + 2) % 3) * sin) * radius;
        }

        /// <summary>The ring of the handle closest to the ray within the angular tolerance.</summary>
        public static bool TryPickRing(float3 origin, float radius, CageHandle handle, CageRay ray, float tolerance,
            out int part)
        {
            var pick = new ClosestPick(tolerance);
            for (int axis = 0; axis < AxisCount; axis++)
            {
                if (!CanRotate(handle, axis))
                    continue;
                float3 previous = RingPoint(origin, radius, axis, 0);
                for (int i = 1; i <= RingSegments; i++)
                {
                    float3 next = RingPoint(origin, radius, axis, i);
                    float distance = RayGeometry.DistanceToSegment(ray, previous, next, out float along);
                    pick.Offer(Ring(axis), distance, along);
                    previous = next;
                }
            }
            part = pick.Index;
            return pick.Found;
        }
    }
}
