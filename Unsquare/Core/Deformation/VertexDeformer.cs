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
                {
                    float3x3 normalMatrix = math.transpose(math.inverse(jacobian));
                    normals[i] = TransformDirection(normals[i], normalMatrix, transform);
                }
                if (hasTangents)
                {
                    float4 t = tangents[i];
                    tangents[i] = new float4(TransformDirection(t.xyz, jacobian, transform), t.w);
                }
            }
            return bounds;
        }

        private static float3 TransformDirection(float3 local, float3x3 matrix, in SubmeshTransform transform)
        {
            float3 building = math.mul(matrix, transform.DirectionToBuilding(local));
            return math.normalizesafe(transform.DirectionToLocal(building), local);
        }
    }
}
