using System.Collections.Generic;
using Game.Prefabs;
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

        public AttachmentKind Kind => AttachmentKind.Props;

        public AttachmentCount Apply(ObjectGeometryPrefab clone, ObjectGeometryPrefab source, AttachmentContext context)
        {
            var subObjects = PrefabCloning.GetOwnedComponent<ObjectSubObjects>(clone, source);
            if (subObjects == null || subObjects.m_SubObjects == null)
                return new AttachmentCount("props", 0, 0);

            int deformedCount = 0, lotCount = 0, linearCount = 0;
            var footprints = new Dictionary<ObjectPrefab, (bool Linear, LinearFootprint Footprint)>();

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
                bool onGround = info.m_ParentMesh == GroundParentMesh;
                if (!onGround && context.Options.LotPropsFollowGround &&
                    PropAnchor.StandsOnLot(context.GroundY, context.AlignmentBox, info.m_Position))
                {
                    onGround = true;
                    lotCount++;
                }
                var rest = new RigidPose(info.m_Position, info.m_Rotation);
                RigidPose pose;
                if (onGround && context.Options.AlignLinearProps &&
                    TryGetLinearFootprint(info.m_Object, footprints, out var footprint))
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
            return new AttachmentCount($"props ({lotCount} attached on the lot, {linearCount} linear)",
                deformed.Length, deformedCount);
        }

        /// <summary>Measures each prop prefab once per variant; placeholders without meshes are not linear.</summary>
        private static bool TryGetLinearFootprint(ObjectPrefab prop,
            Dictionary<ObjectPrefab, (bool Linear, LinearFootprint Footprint)> cache, out LinearFootprint footprint)
        {
            footprint = default;
            if (prop == null)
                return false;
            if (!cache.TryGetValue(prop, out var entry))
            {
                bool linear = LinearFootprint.TryCreate(
                    CageBoxMeasurer.MeasureMeshes(prop as ObjectGeometryPrefab), out var measured);
                entry = (linear, measured);
                cache[prop] = entry;
            }
            footprint = entry.Footprint;
            return entry.Linear;
        }
    }
}
