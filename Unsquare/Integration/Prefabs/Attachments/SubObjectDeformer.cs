using System.Collections.Generic;
using Game.Prefabs;
using Unity.Mathematics;
using Unsquare.Core.Deformation;

namespace Unsquare.Integration.Prefabs.Attachments
{
    /// <summary>
    /// Moves props (sub-objects) rigidly: attached ones with the building cage, ground ones (and attached ones standing
    /// on the lot, like fences) like the ground (blend or lot cage). SubObjectSystem places them from these poses, so
    /// changing the clone's data moves the instances. Long ground props (fences) turn with their line.
    /// </summary>
    internal sealed class SubObjectDeformer : IAttachmentDeformer
    {
        /// <summary>Sub-objects without a parent mesh stand on the ground (trees, sheds) and snap to terrain.</summary>
        private const int GroundParentMesh = -1;

        /// <summary>Diagnostics: attached props near the ground that stay with the building, once per base prefab.</summary>
        private const int MaxReportedProps = 10;
        private const float ReportHeightMeters = 3f;

        private readonly HashSet<string> _reportedBases = new();

        public AttachmentKind Kind => AttachmentKind.Props;

        public AttachmentCount Apply(ObjectGeometryPrefab clone, ObjectGeometryPrefab source, AttachmentContext context)
        {
            var subObjects = PrefabCloning.GetOwnedComponent<ObjectSubObjects>(clone, source);
            if (subObjects == null || subObjects.m_SubObjects == null)
                return new AttachmentCount("props", 0, 0);

            int deformedCount = 0, lotCount = 0, edgeCount = 0, linearCount = 0, reported = 0;
            bool report = _reportedBases.Add(source.name);
            var bounds = new Dictionary<ObjectPrefab, MinMaxBounds>();

            // New array of copies: the infos may still be shared with the base prefab.
            var deformed = new ObjectSubObjectInfo[subObjects.m_SubObjects.Length];
            for (int i = 0; i < deformed.Length; i++)
            {
                var original = subObjects.m_SubObjects[i];
                if (original == null)
                {
                    deformed[i] = original;
                    continue;
                }
                var info = PrefabCloning.ShallowCopy(original);
                var rest = new RigidPose(info.m_Position, info.m_Rotation);
                var propBounds = MeasureProp(info.m_Object, bounds);
                bool linear = LinearFootprint.TryCreate(propBounds, out var footprint);
                bool onGround = info.m_ParentMesh == GroundParentMesh;
                if (!onGround && context.Options.LotPropsFollowGround)
                {
                    if (PropAnchor.StandsOnLot(context.GroundY, context.AlignmentBox, rest, propBounds))
                        (onGround, lotCount) = (true, lotCount + 1);
                    else if (linear && PropAnchor.RunsAlongLotEdge(context.Box, context.GroundY, rest, propBounds, footprint))
                        (onGround, edgeCount) = (true, edgeCount + 1);
                    else if (report && reported < MaxReportedProps &&
                             PropAnchor.ReachesGround(context.GroundY, rest, propBounds, ReportHeightMeters))
                    {
                        reported++;
                        Report(source, info, propBounds, context);
                    }
                }

                RigidPose pose;
                if (onGround && linear && context.Options.AlignLinearProps)
                {
                    pose = RigidDeformer.PlaceLinear(context.Ground, rest, footprint);
                    linearCount++;
                }
                else
                {
                    var mode = onGround ? PlacementMode.Upright : context.Options.AttachedPropMode;
                    pose = RigidDeformer.Place(onGround ? context.Ground : context.Building, context.AlignmentBox,
                        context.Options.FacadeReachMeters, rest, mode);
                }
                info.m_Position = pose.Position;
                info.m_Rotation = pose.Rotation;
                deformed[i] = info;
                deformedCount++;
            }
            subObjects.m_SubObjects = deformed;
            string kind = $"props ({lotCount} attached on the lot, {edgeCount} along the lot edge, {linearCount} linear)";
            return new AttachmentCount(kind, deformed.Length, deformedCount);
        }

        /// <summary>Measures each prop prefab once per variant; placeholders without meshes come back empty.</summary>
        private static MinMaxBounds MeasureProp(ObjectPrefab prop, Dictionary<ObjectPrefab, MinMaxBounds> cache)
        {
            if (prop == null)
                return MinMaxBounds.Empty;
            if (!cache.TryGetValue(prop, out var bounds))
            {
                bounds = CageBoxMeasurer.MeasureMeshes(prop as ObjectGeometryPrefab);
                cache[prop] = bounds;
            }
            return bounds;
        }

        private static void Report(ObjectGeometryPrefab source, ObjectSubObjectInfo info, MinMaxBounds bounds,
            AttachmentContext context)
        {
            float yaw = math.degrees(FaceAlignment.YawOf(info.m_Rotation));
            string name = info.m_Object == null ? "null" : info.m_Object.name;
            Mod.Log.Info($"Prop {name} on {source.name} stays with the building: parent mesh {info.m_ParentMesh}, " +
                $"at {info.m_Position}, yaw {yaw:F0}°, bounds {bounds}, ground {context.GroundY}, " +
                $"mesh box {context.AlignmentBox}, lot {context.Box}");
        }
    }
}
