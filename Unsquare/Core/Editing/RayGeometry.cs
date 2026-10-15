using Unity.Mathematics;

namespace Unsquare.Core.Editing
{
    /// <summary>Closest-point and intersection queries of a ray against points, segments and triangles.</summary>
    public static class RayGeometry
    {
        private const float Epsilon = 1e-8f;

        /// <summary>Distance from the ray to a point, and how far along the ray the closest point lies.</summary>
        public static float DistanceToPoint(CageRay ray, float3 point, out float along)
        {
            along = math.max(0f, math.dot(point - ray.Origin, ray.Direction));
            return math.distance(ray.PointAt(along), point);
        }

        /// <summary>Distance from the ray to the segment a–b, and how far along the ray the closest point lies.</summary>
        public static float DistanceToSegment(CageRay ray, float3 a, float3 b, out float along)
        {
            float3 d = ray.Direction;
            float3 segment = b - a;
            float3 r = ray.Origin - a;
            float e = math.dot(segment, segment);
            float c = math.dot(d, r);
            float u = 0f;
            if (e < Epsilon)
            {
                along = math.max(0f, -c);
            }
            else
            {
                float f = math.dot(segment, r);
                float bd = math.dot(d, segment);
                float denom = e - bd * bd;
                along = denom > Epsilon * e ? math.max(0f, (bd * f - c * e) / denom) : 0f;
                u = (bd * along + f) / e;
                if (u < 0f)
                {
                    u = 0f;
                    along = math.max(0f, -c);
                }
                else if (u > 1f)
                {
                    u = 1f;
                    along = math.max(0f, bd - c);
                }
            }
            return math.distance(ray.PointAt(along), a + segment * u);
        }

        /// <summary>Where the ray crosses the horizontal plane y = <paramref name="height"/>; false if parallel or behind.</summary>
        public static bool IntersectHorizontalPlane(CageRay ray, float height, out float along)
        {
            along = 0f;
            float dy = ray.Direction.y;
            if (math.abs(dy) < Epsilon)
                return false;
            along = (height - ray.Origin.y) / dy;
            return along > 0f;
        }

        /// <summary>Two-sided ray–triangle test (Möller–Trumbore); hits behind the origin do not count.</summary>
        public static bool IntersectTriangle(CageRay ray, float3 a, float3 b, float3 c, out float along)
        {
            along = 0f;
            float3 ab = b - a;
            float3 ac = c - a;
            float3 p = math.cross(ray.Direction, ac);
            float det = math.dot(ab, p);
            if (math.abs(det) < Epsilon)
                return false;
            float inv = 1f / det;
            float3 t = ray.Origin - a;
            float u = math.dot(t, p) * inv;
            if (u < 0f || u > 1f)
                return false;
            float3 q = math.cross(t, ab);
            float v = math.dot(ray.Direction, q) * inv;
            if (v < 0f || u + v > 1f)
                return false;
            along = math.dot(ac, q) * inv;
            return along > 0f;
        }
    }
}
