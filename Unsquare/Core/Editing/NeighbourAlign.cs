using System;
using System.Collections.Generic;
using Unity.Mathematics;
using Unsquare.Core.Cage;
using Unsquare.Core.Terrain;

namespace Unsquare.Core.Editing
{
    /// <summary>
    /// Aligns a side or the back face of a lot cage to the nearest neighbouring edge on the ground (building space xz).
    /// Face corners only slide along their adjacent edges (or onto neighbour corners); columns keep heights and tilt.
    /// </summary>
    public static class NeighbourAlign
    {
        // A lot clipping into its neighbour may reach this share of its depth behind the face; deeper edges (e.g. the
        // street in front, seen from the back face) belong to the other side.
        private const float MaxOverlapDepthShare = 0.5f;
        private const float ParallelEpsilon = 1e-4f;
        private const int TopBit = 2;

        // The face's two ground corners; the first is the back (sides) or the −X one (back face).
        private static (int First, int Second) FaceCorners(AlignSide side) => side switch
        {
            AlignSide.Left => (1, 5),
            AlignSide.Right => (0, 4),
            AlignSide.Back => (0, 1),
            _ => throw new ArgumentException($"Unknown side {side}.", nameof(side)),
        };

        /// <summary>
        /// Finds the edge nearest to the face that is roughly parallel to it and beside it: outside, or inside up to
        /// half the lot depth when the lot clips into a neighbour. Lot edges must face the face.
        /// </summary>
        /// <param name="nearestOutOfReach">Distance of the nearest edge that only failed the reach, for logs;
        /// infinity if there is none.</param>
        public static bool TryFindNeighbour(CageBox box, CageOffsets offsets, AlignSide side,
            IReadOnlyList<NeighbourEdge> edges, NeighbourAlignOptions options, out NeighbourEdge neighbour,
            out float nearestOutOfReach)
        {
            if (edges == null)
                throw new ArgumentNullException(nameof(edges));
            if (options == null)
                throw new ArgumentNullException(nameof(options));
            neighbour = default;
            nearestOutOfReach = float.PositiveInfinity;
            var face = Face.Of(box, offsets, side);
            if (face.Length < ParallelEpsilon)
                return false;

            float minCos = math.cos(math.radians(options.MaxAngleDegrees));
            float minDistance = -face.Depth * MaxOverlapDepthShare;
            float best = float.MaxValue;
            for (int i = 0; i < edges.Count; i++)
            {
                var edge = edges[i];
                float2 along = edge.B - edge.A;
                float length = math.length(along);
                if (length < ParallelEpsilon || math.abs(math.dot(along / length, face.Direction)) < minCos)
                    continue;
                // An edge facing the same way is the far side of a lot (or a lot behind this one).
                if (!edge.IsRoad && math.dot(edge.Outward, face.Outward) >= 0f)
                    continue;
                if (!TryOverlapMidpoint(face, edge, out float2 mid))
                    continue;
                float distance = math.dot(mid - face.Mid, face.Outward);
                if (distance > options.SearchMeters)
                    nearestOutOfReach = math.min(nearestOutOfReach, distance);
                if (distance < minDistance || distance > options.SearchMeters)
                    continue;
                if (math.abs(distance) < best)
                {
                    best = math.abs(distance);
                    neighbour = edge;
                }
            }
            return best < float.MaxValue;
        }

        /// <summary>Moves the face's corner columns so the face follows the neighbour edge.</summary>
        public static CageOffsets Align(CageBox box, CageOffsets offsets, AlignSide side, NeighbourEdge neighbour,
            AlignMode mode)
        {
            if (offsets == null)
                throw new ArgumentNullException(nameof(offsets));
            var face = Face.Of(box, offsets, side);
            float2 lineDir = neighbour.B - neighbour.A;
            if (math.lengthsq(lineDir) < ParallelEpsilon * ParallelEpsilon)
                return offsets;

            float2 first = face.First, second = face.Second;
            switch (mode)
            {
                case AlignMode.Angle:
                    bool keepFirst = LineDistance(neighbour.A, lineDir, face.First) <=
                        LineDistance(neighbour.A, lineDir, face.Second);
                    if (keepFirst)
                        Intersect(face.First, lineDir, face.SecondAnchor, face.Second - face.SecondAnchor, ref second);
                    else
                        Intersect(face.Second, lineDir, face.FirstAnchor, face.First - face.FirstAnchor, ref first);
                    break;
                case AlignMode.Full when !neighbour.IsRoad:
                    bool aFirst = math.dot(neighbour.A - neighbour.B, face.Direction) <= 0f;
                    first = aFirst ? neighbour.A : neighbour.B;
                    second = aFirst ? neighbour.B : neighbour.A;
                    break;
                default:
                    Intersect(neighbour.A, lineDir, face.FirstAnchor, face.First - face.FirstAnchor, ref first);
                    Intersect(neighbour.A, lineDir, face.SecondAnchor, face.Second - face.SecondAnchor, ref second);
                    break;
            }
            var (firstCorner, secondCorner) = FaceCorners(side);
            var aligned = ShiftColumn(offsets, firstCorner, first - face.First);
            return ShiftColumn(aligned, secondCorner, second - face.Second);
        }

        /// <summary>
        /// Moves the face's two corner columns up or down so their ground sits at the neighbour's ground height next to
        /// them. Column heights, xz and the other columns stay.
        /// </summary>
        public static CageOffsets FitHeight(CageBox box, CageOffsets offsets, AlignSide side, NeighbourEdge neighbour)
        {
            var points = GroundFit.GroundPoints3(box, offsets);
            var heights = new float[points.Length];
            for (int i = 0; i < points.Length; i++)
                heights[i] = points[i].y;
            var (first, second) = FaceCorners(side);
            heights[GroundIndex(first)] = neighbour.HeightNear(points[GroundIndex(first)].xz);
            heights[GroundIndex(second)] = neighbour.HeightNear(points[GroundIndex(second)].xz);
            return GroundFit.Fit(box, offsets, heights, GroundFitMode.AllCorners);
        }

        // Same xz shift in grid steps for the bottom and top corner keeps the column's tilt and height.
        private static CageOffsets ShiftColumn(CageOffsets offsets, int bottom, float2 delta)
        {
            int2 steps = CageOffsets.Quantize(new float3(delta.x, 0f, delta.y)).xz;
            var shift = new int3(steps.x, 0, steps.y);
            return offsets.WithCornerSteps(bottom, offsets.Steps(bottom) + shift)
                .WithCornerSteps(bottom | TopBit, offsets.Steps(bottom | TopBit) + shift);
        }

        // Midpoint of the part of the edge that lies beside the face (projected onto the face direction).
        private static bool TryOverlapMidpoint(in Face face, NeighbourEdge edge, out float2 mid)
        {
            mid = default;
            float a = math.dot(edge.A - face.First, face.Direction);
            float b = math.dot(edge.B - face.First, face.Direction);
            if (math.abs(b - a) < ParallelEpsilon)
                return false;
            float from = math.max(math.min(a, b), 0f), to = math.min(math.max(a, b), face.Length);
            if (to <= from)
                return false;
            float t = ((from + to) * 0.5f - a) / (b - a);
            mid = math.lerp(edge.A, edge.B, t);
            return true;
        }

        private static float LineDistance(float2 origin, float2 dir, float2 p) =>
            math.abs(Cross(dir, p - origin)) / math.length(dir);

        // Where line p + t·r meets line q + u·s; leaves the point unchanged for parallel lines.
        private static void Intersect(float2 p, float2 r, float2 q, float2 s, ref float2 point)
        {
            float denom = Cross(r, s);
            if (math.abs(denom) < ParallelEpsilon * math.length(r) * math.length(s))
                return;
            float u = Cross(q - p, r) / denom;
            point = q + u * s;
        }

        private static float Cross(float2 a, float2 b) => a.x * b.y - a.y * b.x;

        private static int GroundIndex(int corner) => (corner & 1) | ((corner >> 2) << 1);

        /// <summary>Ground points of the face and of the corners across from it (other end of each side edge).</summary>
        private readonly struct Face
        {
            private Face(float2 first, float2 second, float2 firstAnchor, float2 secondAnchor)
            {
                First = first;
                Second = second;
                FirstAnchor = firstAnchor;
                SecondAnchor = secondAnchor;
                Length = math.distance(first, second);
                Direction = Length > 0f ? (second - first) / Length : float2.zero;
                Mid = (first + second) * 0.5f;
                float2 normal = new(-Direction.y, Direction.x);
                float2 inside = (firstAnchor + secondAnchor) * 0.5f - Mid;
                Outward = math.dot(normal, inside) > 0f ? -normal : normal;
                Depth = math.abs(math.dot(inside, Outward));
            }

            public float2 First { get; }
            public float2 Second { get; }
            public float2 FirstAnchor { get; }
            public float2 SecondAnchor { get; }
            public float Length { get; }
            public float2 Direction { get; }
            public float2 Mid { get; }
            public float2 Outward { get; }

            /// <summary>How far the lot reaches behind the face.</summary>
            public float Depth { get; }

            public static Face Of(CageBox box, CageOffsets offsets, AlignSide side)
            {
                var points = GroundFit.GroundPoints(box, offsets);
                var (first, second) = FaceCorners(side);
                int across = side == AlignSide.Back ? 4 : 1;
                return new Face(points[GroundIndex(first)], points[GroundIndex(second)],
                    points[GroundIndex(first ^ across)], points[GroundIndex(second ^ across)]);
            }
        }
    }
}
