using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unsquare.Integration.Meshes
{
    /// <summary>
    /// Reads and writes one 32-bit-per-vertex attribute in place, keeping the mesh's vertex layout.
    /// Used for the game's octahedral-packed normals (SNorm16 × 2) and tangents (Float32 × 1).
    /// </summary>
    internal static class PackedVertexChannel
    {
        private const MeshUpdateFlags WriteFlags =
            MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontRecalculateBounds;

        public static bool Has(Mesh mesh, VertexAttribute attribute, VertexAttributeFormat format, int dimension) =>
            mesh.HasVertexAttribute(attribute)
            && mesh.GetVertexAttributeFormat(attribute) == format
            && mesh.GetVertexAttributeDimension(attribute) == dimension;

        public static uint[] Read(Mesh mesh, VertexAttribute attribute)
        {
            using var meshes = Mesh.AcquireReadOnlyMeshData(mesh);
            var data = meshes[0];
            int stream = data.GetVertexAttributeStream(attribute);
            int offset = data.GetVertexAttributeOffset(attribute);
            int stride = data.GetVertexBufferStride(stream);
            var bytes = data.GetVertexData<byte>(stream);

            var words = new uint[data.vertexCount];
            for (int i = 0; i < words.Length; i++)
            {
                int at = i * stride + offset;
                words[i] = bytes[at] | (uint)bytes[at + 1] << 8 | (uint)bytes[at + 2] << 16 | (uint)bytes[at + 3] << 24;
            }
            return words;
        }

        /// <summary>Rewrites the attribute's stream with the words patched in; other attributes are copied as is.</summary>
        public static void Write(Mesh mesh, VertexAttribute attribute, uint[] words)
        {
            int stream, offset, stride;
            NativeArray<byte> bytes;
            using (var meshes = Mesh.AcquireReadOnlyMeshData(mesh))
            {
                var data = meshes[0];
                stream = data.GetVertexAttributeStream(attribute);
                offset = data.GetVertexAttributeOffset(attribute);
                stride = data.GetVertexBufferStride(stream);
                bytes = new NativeArray<byte>(data.GetVertexData<byte>(stream), Allocator.Temp);
            }
            try
            {
                for (int i = 0; i < words.Length; i++)
                {
                    int at = i * stride + offset;
                    uint w = words[i];
                    bytes[at] = (byte)w;
                    bytes[at + 1] = (byte)(w >> 8);
                    bytes[at + 2] = (byte)(w >> 16);
                    bytes[at + 3] = (byte)(w >> 24);
                }
                mesh.SetVertexBufferData(bytes, 0, 0, bytes.Length, stream, WriteFlags);
            }
            finally
            {
                bytes.Dispose();
            }
        }
    }
}
