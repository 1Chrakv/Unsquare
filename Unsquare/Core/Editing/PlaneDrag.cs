using Unity.Mathematics;

namespace Unsquare.Core.Editing
{
    /// <summary>
    /// Maps mouse rays onto the horizontal plane through the grabbed spot: the delta is the movement of the ray's
    /// hit on that plane since the press, so heights never change and the grabbed spot stays under the mouse.
    /// </summary>
    public sealed class PlaneDrag : IHandleDrag
    {
        // Below ~3° between ray and plane the hit runs off to the horizon; such frames are skipped.
        private const float MinSine = 0.05f;

        private readonly float _height;
        private readonly float3 _start;

        private PlaneDrag(float height, float3 start)
        {
            _height = height;
            _start = start;
        }

        /// <param name="height">Building-space height of the grabbed spot.</param>
        public static bool TryStart(float height, CageRay ray, out PlaneDrag drag)
        {
            if (!TryHit(height, ray, out var start))
            {
                drag = null;
                return false;
            }
            drag = new PlaneDrag(height, start);
            return true;
        }

        public bool TryDelta(CageRay ray, out float3 delta)
        {
            if (!TryHit(_height, ray, out var hit))
            {
                delta = float3.zero;
                return false;
            }
            delta = new float3(hit.x - _start.x, 0f, hit.z - _start.z);
            return true;
        }

        private static bool TryHit(float height, CageRay ray, out float3 hit)
        {
            hit = float3.zero;
            if (math.abs(ray.Direction.y) < MinSine || !RayGeometry.IntersectHorizontalPlane(ray, height, out float along))
                return false;
            hit = ray.PointAt(along);
            return true;
        }
    }
}
