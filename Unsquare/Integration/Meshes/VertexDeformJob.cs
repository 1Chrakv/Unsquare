using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unsquare.Core.Deformation;

namespace Unsquare.Integration.Meshes
{
    /// <summary>
    /// Deforms vertices in place in their raw vertex streams: Float32×3 positions, octahedral-packed normals and
    /// tangents. One index is a chunk of <see cref="ChunkSize"/> vertices, which also yields that chunk's bounds.
    /// </summary>
    [BurstCompile]
    internal unsafe struct VertexDeformJob : IJobParallelFor
    {
        public const int ChunkSize = 2048;

        public CageKernel Cage;
        public SubmeshTransform Transform;
        public int VertexCount;

        // Each points at the attribute of vertex 0; null when the mesh has no such attribute.
        [NativeDisableUnsafePtrRestriction] public byte* Positions;
        [NativeDisableUnsafePtrRestriction] public byte* Normals;
        [NativeDisableUnsafePtrRestriction] public byte* Tangents;
        public int PositionStride;
        public int NormalStride;
        public int TangentStride;

        [WriteOnly] public NativeArray<float3> Mins;
        [WriteOnly] public NativeArray<float3> Maxs;

        public static int ChunkCount(int vertexCount) => (vertexCount + ChunkSize - 1) / ChunkSize;

        public void Execute(int chunk)
        {
            int start = chunk * ChunkSize;
            int end = math.min(start + ChunkSize, VertexCount);
            bool hasNormals = Normals != null, hasTangents = Tangents != null;
            var min = new float3(float.MaxValue);
            var max = new float3(float.MinValue);

            for (int i = start; i < end; i++)
            {
                float3 position = UnsafeUtility.ReadArrayElementWithStride<float3>(Positions, i, PositionStride);
                float3 normal = default;
                float4 tangent = default;
                if (hasNormals)
                {
                    normal = OctahedralPacking.UnpackNormal(
                        UnsafeUtility.ReadArrayElementWithStride<uint>(Normals, i, NormalStride));
                }
                if (hasTangents)
                {
                    tangent = OctahedralPacking.UnpackTangent(
                        UnsafeUtility.ReadArrayElementWithStride<uint>(Tangents, i, TangentStride));
                }

                VertexDeformer.DeformVertex(Cage, Transform, ref position, ref normal, ref tangent, hasNormals,
                    hasTangents);

                UnsafeUtility.WriteArrayElementWithStride(Positions, i, PositionStride, position);
                if (hasNormals)
                {
                    UnsafeUtility.WriteArrayElementWithStride(Normals, i, NormalStride,
                        OctahedralPacking.PackNormal(normal));
                }
                if (hasTangents)
                {
                    UnsafeUtility.WriteArrayElementWithStride(Tangents, i, TangentStride,
                        OctahedralPacking.PackTangent(tangent));
                }
                min = math.min(min, position);
                max = math.max(max, position);
            }
            Mins[chunk] = min;
            Maxs[chunk] = max;
        }
    }
}
