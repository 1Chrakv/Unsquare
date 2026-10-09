using Game.Prefabs;
using Unsquare.Core.Deformation;

namespace Unsquare.Integration.Prefabs.Attachments
{
    /// <summary>
    /// Deforms lot nets (paths, …) in the variant's prefab data. Existing net entities are updated separately
    /// after the swap, since the game creates them once at placement.
    /// </summary>
    internal sealed class SubNetDeformer : IAttachmentDeformer
    {
        public AttachmentKind Kind => AttachmentKind.Nets;

        public AttachmentCount Apply(ObjectGeometryPrefab clone, ObjectGeometryPrefab source, AttachmentContext context)
        {
            var subNets = PrefabCloning.GetOwnedComponent<ObjectSubNets>(clone, source);
            if (subNets == null || subNets.m_SubNets == null)
                return new AttachmentCount("nets", 0, 0);

            int deformedCount = 0;
            var result = new ObjectSubNetInfo[subNets.m_SubNets.Length];
            for (int i = 0; i < result.Length; i++)
            {
                var original = subNets.m_SubNets[i];
                result[i] = original;
                if (original == null)
                    continue;
                var info = PrefabCloning.ShallowCopy(original);
                bool onGround = info.m_ParentMesh.x < 0 && info.m_ParentMesh.y < 0;
                info.m_BezierCurve = CurveConversions.Deform(context, info.m_BezierCurve, onGround);
                result[i] = info;
                deformedCount++;
            }
            subNets.m_SubNets = result;
            return new AttachmentCount("nets", result.Length, deformedCount);
        }
    }
}
