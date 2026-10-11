using System;
using Unity.Mathematics;

namespace Unsquare.Core.Editing
{
    /// <summary>
    /// The move gizmo: three arrows along the building axes X, Y, Z (0, 1, 2) starting at the selected handle.
    /// Shared by picking and drawing so both agree on its size.
    /// </summary>
    public static class GizmoAxes
    {
        public const int AxisCount = 3;

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
    }
}
