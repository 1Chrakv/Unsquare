using Unity.Mathematics;
using Unsquare.Core.Cage;

namespace Unsquare.Core.Editing
{
    /// <summary>
    /// Finds the cage handle under a mouse ray. Edges within the angular tolerance win over faces, so the
    /// border of a face grabs its edge; corners are only picked on request (Ctrl).
    /// </summary>
    public static class CagePicker
    {
        /// <param name="tolerance">Pick radius as an angular size (metres per metre of camera distance).</param>
        /// <param name="corners">Pick only corners instead of edges and faces.</param>
        public static bool TryPick(CageBox box, CageOffsets offsets, CageRay ray, float tolerance, bool corners,
            out CageHandle handle)
        {
            if (corners)
                return TryPickCorner(box, offsets, ray, tolerance, out handle);
            return TryPickEdge(box, offsets, ray, tolerance, out handle) ||
                   TryPickFace(box, offsets, ray, out handle);
        }

        public static bool TryPickCorner(CageBox box, CageOffsets offsets, CageRay ray, float tolerance,
            out CageHandle handle)
        {
            var pick = new ClosestPick(tolerance);
            for (int i = 0; i < CageTopology.CornerCount; i++)
            {
                float distance = RayGeometry.DistanceToPoint(ray, DeformedCage.CornerPosition(box, offsets, i),
                    out float along);
                pick.Offer(i, distance, along);
            }
            handle = pick.Found ? CageHandle.Corner(pick.Index) : default;
            return pick.Found;
        }

        public static bool TryPickEdge(CageBox box, CageOffsets offsets, CageRay ray, float tolerance,
            out CageHandle handle)
        {
            var pick = new ClosestPick(tolerance);
            for (int i = 0; i < CageTopology.EdgeCount; i++)
            {
                var corners = CageTopology.CornersOf(CageHandle.Edge(i));
                float distance = RayGeometry.DistanceToSegment(ray,
                    DeformedCage.CornerPosition(box, offsets, corners[0]),
                    DeformedCage.CornerPosition(box, offsets, corners[1]), out float along);
                pick.Offer(i, distance, along);
            }
            handle = pick.Found ? CageHandle.Edge(pick.Index) : default;
            return pick.Found;
        }

        /// <summary>The face the ray enters first; deformed faces are split into two triangles.</summary>
        public static bool TryPickFace(CageBox box, CageOffsets offsets, CageRay ray, out CageHandle handle)
        {
            int best = -1;
            float bestAlong = float.MaxValue;
            for (int i = 0; i < CageTopology.FaceCount; i++)
            {
                // Face corners come in bit order of the two in-plane axes; 0-1-3-2 walks around the quad.
                var corners = CageTopology.CornersOf(CageHandle.Face(i));
                float3 a = DeformedCage.CornerPosition(box, offsets, corners[0]);
                float3 b = DeformedCage.CornerPosition(box, offsets, corners[1]);
                float3 c = DeformedCage.CornerPosition(box, offsets, corners[3]);
                float3 d = DeformedCage.CornerPosition(box, offsets, corners[2]);
                if ((RayGeometry.IntersectTriangle(ray, a, b, c, out float along) ||
                     RayGeometry.IntersectTriangle(ray, a, c, d, out along)) && along < bestAlong)
                {
                    best = i;
                    bestAlong = along;
                }
            }
            handle = best >= 0 ? CageHandle.Face(best) : default;
            return best >= 0;
        }
    }
}
