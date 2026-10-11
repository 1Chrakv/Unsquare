using Unity.Mathematics;
using Unsquare.Core.Cage;

namespace Unsquare.Core.Editing
{
    /// <summary>
    /// Whether a mouse ray passes close to a (deformed) cage: through its bounds grown by a margin. Lets a
    /// slightly missed click keep the selection while a click far away releases it.
    /// </summary>
    public static class CageProximity
    {
        /// <param name="marginFraction">Margin as a part of the cage's largest size, e.g. 0.05.</param>
        public static bool IsNear(CageBox box, CageOffsets offsets, CageRay ray, float marginFraction)
        {
            float3 min = DeformedCage.CornerPosition(box, offsets, 0);
            float3 max = min;
            for (int i = 1; i < CageTopology.CornerCount; i++)
            {
                float3 corner = DeformedCage.CornerPosition(box, offsets, i);
                min = math.min(min, corner);
                max = math.max(max, corner);
            }
            float margin = marginFraction * math.cmax(max - min);
            return HitsBox(ray, min - margin, max + margin);
        }

        // Slab test; only hits in front of the ray origin count (the origin may lie inside the box).
        private static bool HitsBox(CageRay ray, float3 min, float3 max)
        {
            float near = 0f;
            float far = float.MaxValue;
            for (int axis = 0; axis < 3; axis++)
            {
                float origin = ray.Origin[axis];
                float direction = ray.Direction[axis];
                if (math.abs(direction) < 1e-8f)
                {
                    if (origin < min[axis] || origin > max[axis])
                        return false;
                    continue;
                }
                float t1 = (min[axis] - origin) / direction;
                float t2 = (max[axis] - origin) / direction;
                near = math.max(near, math.min(t1, t2));
                far = math.min(far, math.max(t1, t2));
                if (near > far)
                    return false;
            }
            return true;
        }
    }
}
