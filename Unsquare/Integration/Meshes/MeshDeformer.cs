using System;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using Unsquare.Core.Deformation;
using Unsquare.Core.Diagnostics;
using Object = UnityEngine.Object;

namespace Unsquare.Integration.Meshes
{
    /// <summary>
    /// Bridges UnityEngine.Mesh and the pure <see cref="VertexDeformer"/>.
    /// Always works on a copy; the caller owns (and must destroy) the returned meshes.
    /// </summary>
    internal static class MeshDeformer
    {
        public static Mesh[] DeformCopies(Mesh[] sources, in SubmeshTransform transform, IDeformation deformation,
            PhaseTimer timer, out MinMaxBounds bounds)
        {
            var result = new Mesh[sources.Length];
            bounds = MinMaxBounds.Empty;
            try
            {
                for (int i = 0; i < sources.Length; i++)
                {
                    result[i] = DeformCopy(sources[i], transform, deformation, timer, out var meshBounds);
                    bounds = bounds.Encapsulate(meshBounds);
                }
                return result;
            }
            catch
            {
                Destroy(result);
                throw;
            }
        }

        public static void Destroy(Mesh[] meshes)
        {
            foreach (var mesh in meshes)
            {
                if (mesh != null)
                    Object.Destroy(mesh);
            }
        }

        private static Mesh DeformCopy(Mesh source, in SubmeshTransform transform, IDeformation deformation,
            PhaseTimer timer, out MinMaxBounds bounds)
        {
            if (source == null)
                throw new ArgumentException("Render prefab returned a null mesh.");
            if (!source.isReadable)
                throw new InvalidOperationException($"Mesh {source.name} is not CPU readable.");

            if (!HasFloatChannel(source, VertexAttribute.Position, 3))
                throw new InvalidOperationException($"Mesh {source.name} has packed positions.");

            var reading = timer.Measure(BakePhase.Read);
            Mesh mesh = Object.Instantiate(source);
            mesh.name = source.name;
            Vector3[] vertices = mesh.vertices;
            int count = vertices.Length;
            // Game meshes pack normals/tangents octahedrally (_TANGENTSPACE_OCTO). The simple Mesh API can't
            // read them and its setters would change the layout the shader decodes, so packed ones go raw.
            bool floatNormals = HasFloatChannel(mesh, VertexAttribute.Normal, 3);
            bool packedNormals = !floatNormals
                && PackedVertexChannel.Has(mesh, VertexAttribute.Normal, VertexAttributeFormat.SNorm16, 2);
            bool floatTangents = HasFloatChannel(mesh, VertexAttribute.Tangent, 4);
            bool packedTangents = !floatTangents
                && PackedVertexChannel.Has(mesh, VertexAttribute.Tangent, VertexAttributeFormat.Float32, 1);

            var positions = new float3[count];
            for (int i = 0; i < count; i++)
                positions[i] = vertices[i];
            float3[] n = floatNormals ? ToFloat3(mesh.normals, count)
                : packedNormals ? UnpackNormals(PackedVertexChannel.Read(mesh, VertexAttribute.Normal)) : null;
            float4[] t = floatTangents ? ToFloat4(mesh.tangents, count)
                : packedTangents ? UnpackTangents(PackedVertexChannel.Read(mesh, VertexAttribute.Tangent)) : null;
            reading.Dispose();
            timer.Count(BakePhase.Vertices, count);
            timer.Count(BakePhase.Meshes, 1);

            using (timer.Measure(BakePhase.Deform))
                bounds = VertexDeformer.Deform(positions, n, t, transform, deformation);

            uint[] packedNormalWords = null, packedTangentWords = null;
            using (timer.Measure(BakePhase.Pack))
            {
                if (n != null && !floatNormals)
                    packedNormalWords = PackNormals(n);
                if (t != null && !floatTangents)
                    packedTangentWords = PackTangents(t);
            }

            using (timer.Measure(BakePhase.Write))
            {
                for (int i = 0; i < count; i++)
                    vertices[i] = positions[i];
                mesh.vertices = vertices;
                if (packedNormalWords != null)
                    PackedVertexChannel.Write(mesh, VertexAttribute.Normal, packedNormalWords);
                else if (n != null)
                    mesh.normals = ToVector3(n);
                if (packedTangentWords != null)
                    PackedVertexChannel.Write(mesh, VertexAttribute.Tangent, packedTangentWords);
                else if (t != null)
                    mesh.tangents = ToVector4(t);
                mesh.RecalculateBounds();
            }
            return mesh;
        }

        private static bool HasFloatChannel(Mesh mesh, VertexAttribute attribute, int dimension) =>
            PackedVertexChannel.Has(mesh, attribute, VertexAttributeFormat.Float32, dimension);

        private static float3[] UnpackNormals(uint[] words)
        {
            var result = new float3[words.Length];
            for (int i = 0; i < words.Length; i++)
                result[i] = OctahedralPacking.UnpackNormal(words[i]);
            return result;
        }

        private static float4[] UnpackTangents(uint[] words)
        {
            var result = new float4[words.Length];
            for (int i = 0; i < words.Length; i++)
                result[i] = OctahedralPacking.UnpackTangent(words[i]);
            return result;
        }

        private static uint[] PackNormals(float3[] normals)
        {
            var result = new uint[normals.Length];
            for (int i = 0; i < normals.Length; i++)
                result[i] = OctahedralPacking.PackNormal(normals[i]);
            return result;
        }

        private static uint[] PackTangents(float4[] tangents)
        {
            var result = new uint[tangents.Length];
            for (int i = 0; i < tangents.Length; i++)
                result[i] = OctahedralPacking.PackTangent(tangents[i]);
            return result;
        }

        private static Vector3[] ToVector3(float3[] source)
        {
            var result = new Vector3[source.Length];
            for (int i = 0; i < source.Length; i++)
                result[i] = source[i];
            return result;
        }

        private static Vector4[] ToVector4(float4[] source)
        {
            var result = new Vector4[source.Length];
            for (int i = 0; i < source.Length; i++)
                result[i] = source[i];
            return result;
        }

        private static float3[] ToFloat3(Vector3[] source, int expected)
        {
            if (source == null || source.Length != expected)
                return null;
            var result = new float3[source.Length];
            for (int i = 0; i < source.Length; i++)
                result[i] = source[i];
            return result;
        }

        private static float4[] ToFloat4(Vector4[] source, int expected)
        {
            if (source == null || source.Length != expected)
                return null;
            var result = new float4[source.Length];
            for (int i = 0; i < source.Length; i++)
                result[i] = source[i];
            return result;
        }
    }
}
