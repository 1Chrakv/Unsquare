using System.Collections.Generic;
using Colossal.Mathematics;
using Game.Prefabs;
using Unity.Mathematics;
using Unsquare.Core.Deformation;

namespace Unsquare.Integration.Prefabs.Attachments
{
    /// <summary>
    /// Deforms lot lanes (fences, hedges, …). Ends on a mesh follow the building unless they stand on the lot
    /// (assets attach garden fences to the house). LaneSystem rebuilds object lanes on update, so the swap moves them.
    /// </summary>
    internal sealed class SubLaneDeformer : IAttachmentDeformer
    {
        /// <summary>Diagnostics: attached lane ends near the ground that stay with the building, once per base.</summary>
        private const int MaxReportedLanes = 10;
        private const float ReportHeightMeters = 3f;

        private readonly HashSet<string> _reportedBases = new();

        public AttachmentKind Kind => AttachmentKind.Lanes;

        public AttachmentCount Apply(ObjectGeometryPrefab clone, ObjectGeometryPrefab source, AttachmentContext context)
        {
            var subLanes = PrefabCloning.GetOwnedComponent<ObjectSubLanes>(clone, source);
            if (subLanes == null || subLanes.m_SubLanes == null)
                return new AttachmentCount("lanes", 0, 0);

            int deformedCount = 0, lotEnds = 0, reported = 0;
            bool report = _reportedBases.Add(source.name);
            var result = new ObjectSubLaneInfo[subLanes.m_SubLanes.Length];
            for (int i = 0; i < result.Length; i++)
            {
                var original = subLanes.m_SubLanes[i];
                result[i] = original;
                if (original == null)
                    continue;
                var info = PrefabCloning.ShallowCopy(original);
                var curve = info.m_BezierCurve;
                bool startAttached = info.m_ParentMesh.x >= 0, endAttached = info.m_ParentMesh.y >= 0;
                if (context.Options.LotPropsFollowGround)
                {
                    if (startAttached && PropAnchor.StandsOnLot(context.GroundY, context.AlignmentBox, curve.a))
                        (startAttached, lotEnds) = (false, lotEnds + 1);
                    if (endAttached && PropAnchor.StandsOnLot(context.GroundY, context.AlignmentBox, curve.d))
                        (endAttached, lotEnds) = (false, lotEnds + 1);
                    if (report && (startAttached || endAttached) && reported < MaxReportedLanes &&
                        math.min(curve.a.y, curve.d.y) - context.GroundY < ReportHeightMeters)
                    {
                        reported++;
                        Report(source, info, curve, context);
                    }
                }
                bool groundCurve = info.m_ParentMesh.x < 0 && info.m_ParentMesh.y < 0;
                info.m_BezierCurve = CurveConversions.Deform(context, curve, startAttached, endAttached, groundCurve);
                result[i] = info;
                deformedCount++;
            }
            subLanes.m_SubLanes = result;
            return new AttachmentCount($"lanes ({lotEnds} ends on the lot)", result.Length, deformedCount);
        }

        private static void Report(ObjectGeometryPrefab source, ObjectSubLaneInfo info, Bezier4x3 curve,
            AttachmentContext context)
        {
            string name = info.m_LanePrefab == null ? "null" : info.m_LanePrefab.name;
            Mod.Log.Info($"Lane {name} on {source.name} stays with the building: parent mesh {info.m_ParentMesh}, " +
                $"from {curve.a} to {curve.d}, ground {context.GroundY}, mesh box {context.AlignmentBox}");
        }
    }
}
