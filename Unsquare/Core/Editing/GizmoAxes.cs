using System;
using Unity.Mathematics;

namespace Unsquare.Core.Editing
{
    /// <summary>
    /// The move gizmo: three arrows along the building axes X, Y, Z (0, 1, 2) starting at the selected handle and
    /// a square between the X and Z arrows (<see cref="PlaneXZ"/>). Shared by picking and drawing.
    /// </summary>
    public static class GizmoAxes
    {
        public const int AxisCount = 3;

        /// <summary>Gizmo part id of the horizontal plane square, after the three axes.</summary>
        public const int PlaneXZ = 3;

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
    }
}
