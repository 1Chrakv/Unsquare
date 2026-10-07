using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace Unsquare.Core.Cage
{
    /// <summary>
    /// Static corner/edge/face tables of the cuboid. Corner index bits: x = 1, y = 2, z = 4 (set = max side).
    /// Edges are grouped by axis (X, Y, Z; 4 each). Faces: -X, +X, -Y, +Y, -Z, +Z.
    /// </summary>
    public static class CageTopology
    {
        public const int CornerCount = 8;
        public const int EdgeCount = 12;
        public const int FaceCount = 6;

        private static readonly int[][] CornerSets = BuildCornerSets();
        private static readonly int[][] EdgeSets = BuildEdgeSets();
        private static readonly int[][] FaceSets = BuildFaceSets();

        public static int CountOf(CageHandleKind kind) => kind switch
        {
            CageHandleKind.Corner => CornerCount,
            CageHandleKind.Edge => EdgeCount,
            CageHandleKind.Face => FaceCount,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };

        /// <summary>0/1 per axis: which side of the box the corner lies on.</summary>
        public static int3 CornerSide(int corner) => new(corner & 1, (corner >> 1) & 1, (corner >> 2) & 1);

        /// <summary>Corner indices moved together by a handle.</summary>
        public static IReadOnlyList<int> CornersOf(CageHandle handle) => handle.Kind switch
        {
            CageHandleKind.Corner => CornerSets[handle.Index],
            CageHandleKind.Edge => EdgeSets[handle.Index],
            CageHandleKind.Face => FaceSets[handle.Index],
            _ => throw new ArgumentOutOfRangeException(nameof(handle)),
        };

        /// <summary>Axis (0..2) along which an edge runs.</summary>
        public static int EdgeAxis(int edge) => edge / 4;

        /// <summary>Axis (0..2) a face is perpendicular to, and its side (0 = min, 1 = max).</summary>
        public static int FaceAxis(int face) => face / 2;
        public static int FaceSide(int face) => face % 2;

        private static int[][] BuildCornerSets()
        {
            var sets = new int[CornerCount][];
            for (int i = 0; i < CornerCount; i++)
                sets[i] = new[] { i };
            return sets;
        }

        private static int[][] BuildEdgeSets()
        {
            var sets = new int[EdgeCount][];
            for (int axis = 0; axis < 3; axis++)
            {
                int bit = 1 << axis;
                int n = 0;
                for (int corner = 0; corner < CornerCount; corner++)
                {
                    if ((corner & bit) != 0)
                        continue;
                    sets[axis * 4 + n++] = new[] { corner, corner | bit };
                }
            }
            return sets;
        }

        private static int[][] BuildFaceSets()
        {
            var sets = new int[FaceCount][];
            for (int face = 0; face < FaceCount; face++)
            {
                int bit = 1 << FaceAxis(face);
                int want = FaceSide(face) == 1 ? bit : 0;
                var corners = new List<int>(4);
                for (int corner = 0; corner < CornerCount; corner++)
                {
                    if ((corner & bit) == want)
                        corners.Add(corner);
                }
                sets[face] = corners.ToArray();
            }
            return sets;
        }
    }
}
