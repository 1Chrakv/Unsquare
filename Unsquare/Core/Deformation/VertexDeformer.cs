using System;
using Unity.Mathematics;

namespace Unsquare.Core.Deformation
{
    /// <summary>
    /// Deforms vertex arrays of one submesh in place. Normals use the inverse-transpose Jacobian,
    /// tangents the Jacobian itself; tangent handedness (w) is kept.
    /// </summary>
    public static class VertexDeformer
    {
        private const float MinDeterminant = 1e-6f;

        /// <param name="normals">Optional; ignored unless it matches the vertex count.</param>
        /// <param name="tangents">Optional; ignored unless it matches the vertex count.</param>
        /// <returns>Bounds of the deformed positions in submesh-local space.</returns>
        public static MinMaxBounds Deform(
            float3[] positions, float3[] normals, float4[] tangents,
            in SubmeshTransform transform, IDeformation deformation)
        {
            if (positions == null)
                throw new ArgumentNullException(nameof(positions));
            if (deformation == null)
                throw new ArgumentNullException(nameof(deformation));

            bool hasNormals = normals != null && normals.Length == positions.Length;
            bool hasTangents = tangents != null && tangents.Length == positions.Length;
            var bounds = MinMaxBounds.Empty;

            for (int i = 0; i < positions.Length; i++)
            {
                float3 building = transform.PointToBuilding(positions[i]);
                float3 moved = deformation.Apply(building, out float3x3 jacobian);
                positions[i] = transform.PointToLocal(moved);
                bounds = bounds.Encapsulate(positions[i]);

                if (math.abs(math.determinant(jacobian)) < MinDeterminant)
                    continue;
                if (hasNormals)
                    normals[i] = DeformNormal(normals[i], jacobian, transform);
                if (hasTangents)
                    tangents[i] = DeformTangent(tangents[i], jacobian, transform);
            }
            return bounds;
        }

        /// <summary>
        /// One vertex through a cage kernel, job-friendly; same result as <see cref="Deform"/> with that cage.
        /// </summary>
        public static void DeformVertex(in CageKernel cage, in SubmeshTransform transform, ref float3 position,
            ref float3 normal, ref float4 tangent, bool hasNormal, bool hasTangent)
        {
            float3 moved = cage.Apply(transform.PointToBuilding(position), out float3x3 jacobian);
            position = transform.PointToLocal(moved);

            if (math.abs(math.determinant(jacobian)) < MinDeterminant)
                return;
            if (hasNormal)
                normal = DeformNormal(normal, jacobian, transform);
            if (hasTangent)
                tangent = DeformTangent(tangent, jacobian, transform);
        }

        private static float3 DeformNormal(float3 normal, in float3x3 jacobian, in SubmeshTransform transform) =>
            TransformDirection(normal, math.transpose(math.inverse(jacobian)), transform);

        private static float4 DeformTangent(float4 tangent, in float3x3 jacobian, in SubmeshTransform transform) =>
            new(TransformDirection(tangent.xyz, jacobian, transform), tangent.w);

        private static float3 TransformDirection(float3 local, float3x3 matrix, in SubmeshTransform transform)
        {
            float3 building = math.mul(matrix, transform.DirectionToBuilding(local));
            return math.normalizesafe(transform.DirectionToLocal(building), local);
        }
    }
}
