using System;
using Unity.Mathematics;
using UnityEngine;
using Unsquare.Core.Deformation;
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
            out MinMaxBounds bounds)
        {
            var result = new Mesh[sources.Length];
            bounds = MinMaxBounds.Empty;
            try
            {
                for (int i = 0; i < sources.Length; i++)
                {
                    result[i] = DeformCopy(sources[i], transform, deformation, out var meshBounds);
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
            out MinMaxBounds bounds)
        {
            if (source == null)
                throw new ArgumentException("Render prefab returned a null mesh.");
            if (!source.isReadable)
                throw new InvalidOperationException($"Mesh {source.name} is not CPU readable.");

            Mesh mesh = Object.Instantiate(source);
            mesh.name = source.name;
            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            Vector4[] tangents = mesh.tangents;

            var positions = new float3[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
                positions[i] = vertices[i];
            float3[] n = ToFloat3(normals, vertices.Length);
            float4[] t = ToFloat4(tangents, vertices.Length);

            bounds = VertexDeformer.Deform(positions, n, t, transform, deformation);

            for (int i = 0; i < vertices.Length; i++)
                vertices[i] = positions[i];
            mesh.vertices = vertices;
            if (n != null)
            {
                for (int i = 0; i < n.Length; i++)
                    normals[i] = n[i];
                mesh.normals = normals;
            }
            if (t != null)
            {
                for (int i = 0; i < t.Length; i++)
                    tangents[i] = t[i];
                mesh.tangents = tangents;
            }
            mesh.RecalculateBounds();
            return mesh;
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
