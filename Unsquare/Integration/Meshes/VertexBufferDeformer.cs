using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using Unsquare.Core.Deformation;

namespace Unsquare.Integration.Meshes
{
    /// <summary>
    /// The fast bake path: copies a mesh and deforms its raw vertex streams with <see cref="VertexDeformJob"/> on
    /// the worker threads, waiting for it in the same frame. Only for the game's packed vertex layout.
    /// </summary>
    internal static class VertexBufferDeformer
    {
        private const int MaxStreams = 4;

        private const MeshUpdateFlags WriteFlags =
            MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontRecalculateBounds;

        /// <summary>Float positions; normals and tangents either missing or octahedral-packed.</summary>
        public static bool Supports(Mesh mesh) =>
            PackedVertexChannel.Has(mesh, VertexAttribute.Position, VertexAttributeFormat.Float32, 3)
            && IsMissingOr(mesh, VertexAttribute.Normal, VertexAttributeFormat.SNorm16, 2)
            && IsMissingOr(mesh, VertexAttribute.Tangent, VertexAttributeFormat.Float32, 1);

        /// <summary>The caller owns (and must destroy) the returned mesh.</summary>
        public static unsafe Mesh DeformCopy(Mesh source, in SubmeshTransform transform, in CageKernel cage,
            out MinMaxBounds bounds)
        {
            Mesh mesh = Object.Instantiate(source);
            mesh.name = source.name;
            var streams = new NativeArray<byte>[MaxStreams];
            NativeArray<float3> mins = default, maxs = default;
            try
            {
                var job = new VertexDeformJob { Cage = cage, Transform = transform };
                using (var meshes = Mesh.AcquireReadOnlyMeshData(mesh))
                {
                    var data = meshes[0];
                    job.VertexCount = data.vertexCount;
                    job.Positions = Attribute(data, VertexAttribute.Position, streams, out job.PositionStride);
                    if (data.HasVertexAttribute(VertexAttribute.Normal))
                        job.Normals = Attribute(data, VertexAttribute.Normal, streams, out job.NormalStride);
                    if (data.HasVertexAttribute(VertexAttribute.Tangent))
                        job.Tangents = Attribute(data, VertexAttribute.Tangent, streams, out job.TangentStride);
                }

                int chunks = VertexDeformJob.ChunkCount(job.VertexCount);
                mins = new NativeArray<float3>(chunks, Allocator.TempJob);
                maxs = new NativeArray<float3>(chunks, Allocator.TempJob);
                job.Mins = mins;
                job.Maxs = maxs;
                job.Schedule(chunks, 1).Complete();

                bounds = MinMaxBounds.Empty;
                for (int i = 0; i < chunks; i++)
                    bounds = bounds.Encapsulate(new MinMaxBounds(mins[i], maxs[i]));

                for (int stream = 0; stream < MaxStreams; stream++)
                {
                    if (streams[stream].IsCreated)
                        mesh.SetVertexBufferData(streams[stream], 0, 0, streams[stream].Length, stream, WriteFlags);
                }
                mesh.RecalculateBounds();
                return mesh;
            }
            catch
            {
                Object.Destroy(mesh);
                throw;
            }
            finally
            {
                for (int stream = 0; stream < MaxStreams; stream++)
                {
                    if (streams[stream].IsCreated)
                        streams[stream].Dispose();
                }
                if (mins.IsCreated)
                    mins.Dispose();
                if (maxs.IsCreated)
                    maxs.Dispose();
            }
        }

        // Copies the attribute's stream once (attributes may share one) and points at the attribute of vertex 0.
        private static unsafe byte* Attribute(Mesh.MeshData data, VertexAttribute attribute,
            NativeArray<byte>[] streams, out int stride)
        {
            int stream = data.GetVertexAttributeStream(attribute);
            stride = data.GetVertexBufferStride(stream);
            if (!streams[stream].IsCreated)
                streams[stream] = new NativeArray<byte>(data.GetVertexData<byte>(stream), Allocator.TempJob);
            return (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(streams[stream])
                   + data.GetVertexAttributeOffset(attribute);
        }

        private static bool IsMissingOr(Mesh mesh, VertexAttribute attribute, VertexAttributeFormat format,
            int dimension) =>
            !mesh.HasVertexAttribute(attribute) || PackedVertexChannel.Has(mesh, attribute, format, dimension);
    }
}
