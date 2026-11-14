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
    /// Debug builds only: bakes the first few meshes of a session a second time on the managed path and logs how
    /// far the vertex job's result is from it.
    /// </summary>
    internal sealed class MeshPathCheck
    {
        private int _remaining = 3;

        /// <summary>Null in release builds: no check, no second bake.</summary>
        public static MeshPathCheck CreateForBuild()
        {
#if DEBUG
            return new MeshPathCheck();
#else
            return null;
#endif
        }

        public void Compare(Mesh source, Mesh fast, in SubmeshTransform transform, IDeformation deformation)
        {
            if (_remaining <= 0)
                return;
            _remaining--;
            Mesh managed = null;
            try
            {
                managed = MeshDeformer.DeformCopyManaged(source, transform, deformation, new PhaseTimer(), out _);
                Vector3[] a = fast.vertices, b = managed.vertices;
                float position = 0f;
                for (int i = 0; i < a.Length; i++)
                    position = math.max(position, Vector3.Distance(a[i], b[i]));
                Mod.Log.Info(FormattableString.Invariant(
                    $"Vertex job check on {source.name}: {a.Length} vertices, max difference to the managed path: ") +
                    FormattableString.Invariant($"position {position:0.######} m, ") +
                    $"normals {Describe(fast, managed, VertexAttribute.Normal, false)}, " +
                    $"tangents {Describe(fast, managed, VertexAttribute.Tangent, true)}");
            }
            catch (Exception e)
            {
                Mod.Log.Warn($"Vertex job check on {source.name} failed: {e.Message}");
            }
            finally
            {
                if (managed != null)
                    Object.Destroy(managed);
            }
        }

        // Largest angle between the two paths' directions, and how many packed words differ at all.
        private static string Describe(Mesh fast, Mesh managed, VertexAttribute attribute, bool tangent)
        {
            if (!fast.HasVertexAttribute(attribute))
                return "none";
            uint[] a = PackedVertexChannel.Read(fast, attribute), b = PackedVertexChannel.Read(managed, attribute);
            float minDot = 1f;
            int different = 0;
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] == b[i])
                    continue;
                different++;
                float3 x = tangent ? OctahedralPacking.UnpackTangent(a[i]).xyz : OctahedralPacking.UnpackNormal(a[i]);
                float3 y = tangent ? OctahedralPacking.UnpackTangent(b[i]).xyz : OctahedralPacking.UnpackNormal(b[i]);
                minDot = math.min(minDot, math.dot(x, y));
            }
            float degrees = math.degrees(math.acos(math.clamp(minDot, -1f, 1f)));
            return FormattableString.Invariant($"{degrees:0.###}° ({different} words differ)");
        }
    }
}
