using Game.Prefabs;
using Unsquare.Core.Deformation;

namespace Unsquare.Integration.Prefabs.Attachments
{
    /// <summary>
    /// Deforms lot lanes (fences, hedges, …). In game mode LaneSystem rebuilds object lanes from the prefab
    /// whenever the building is updated, so the swap alone moves them.
    /// </summary>
    internal sealed class SubLaneDeformer : IAttachmentDeformer
    {
        public AttachmentKind Kind => AttachmentKind.Lanes;

        public AttachmentCount Apply(ObjectGeometryPrefab clone, ObjectGeometryPrefab source, AttachmentContext context)
        {
            var subLanes = PrefabCloning.GetOwnedComponent<ObjectSubLanes>(clone, source);
            if (subLanes == null || subLanes.m_SubLanes == null)
                return new AttachmentCount("lanes", 0, 0);

            int deformedCount = 0;
            var result = new ObjectSubLaneInfo[subLanes.m_SubLanes.Length];
            for (int i = 0; i < result.Length; i++)
            {
                var original = subLanes.m_SubLanes[i];
                result[i] = original;
                if (original == null || !context.Affects(CurveConversions.Middle(original.m_BezierCurve)))
                    continue;
                var info = PrefabCloning.ShallowCopy(original);
                bool onGround = info.m_ParentMesh.x < 0 && info.m_ParentMesh.y < 0;
                info.m_BezierCurve = CurveConversions.Deform(context, info.m_BezierCurve, onGround);
                result[i] = info;
                deformedCount++;
            }
            subLanes.m_SubLanes = result;
            return new AttachmentCount("lanes", result.Length, deformedCount);
        }
    }
}
