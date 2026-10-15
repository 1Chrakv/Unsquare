using Unity.Mathematics;

namespace Unsquare.Core.Editing
{
    /// <summary>
    /// Maps mouse rays onto a gizmo axis: the delta is the movement of the ray's closest point on the axis
    /// line since the press, so the grabbed spot stays under the mouse.
    /// </summary>
    public sealed class AxisDrag : IHandleDrag
    {
        // Below ~3° between ray and axis the projection explodes; such frames are skipped.
        private const float MinSine = 0.05f;

        private readonly float3 _origin;
        private readonly float _start;

        private AxisDrag(float3 origin, int axis, float start)
        {
            _origin = origin;
            Axis = axis;
            Direction = GizmoAxes.Direction(axis);
            _start = start;
        }

        public int Axis { get; }
        public float3 Direction { get; }

        /// <param name="origin">Position of the handle at the press, in building space.</param>
        public static bool TryStart(float3 origin, int axis, CageRay ray, out AxisDrag drag)
        {
            var direction = GizmoAxes.Direction(axis);
            if (!TryProject(origin, direction, ray, out float start))
            {
                drag = null;
                return false;
            }
            drag = new AxisDrag(origin, axis, start);
            return true;
        }

        /// <summary>Total building-space delta since the press; false if the ray runs along the axis.</summary>
        public bool TryDelta(CageRay ray, out float3 delta)
        {
            if (!TryProject(_origin, Direction, ray, out float t))
            {
                delta = float3.zero;
                return false;
            }
            delta = Direction * (t - _start);
            return true;
        }

        // Parameter of the point on the axis line closest to the ray; it must lie in front of the camera.
        private static bool TryProject(float3 origin, float3 axis, CageRay ray, out float t)
        {
            float3 w = origin - ray.Origin;
            float b = math.dot(axis, ray.Direction);
            float denom = 1f - b * b;
            t = 0f;
            if (denom < MinSine * MinSine)
                return false;
            float dw = math.dot(ray.Direction, w);
            t = (b * dw - math.dot(axis, w)) / denom;
            float along = dw + b * t;
            return along > 0f;
        }
    }
}
