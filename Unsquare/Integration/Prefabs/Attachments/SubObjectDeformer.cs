using Game.Prefabs;
using Unsquare.Core.Deformation;

namespace Unsquare.Integration.Prefabs.Attachments
{
    /// <summary>
    /// Moves and rotates the building's props (sub-objects) rigidly: attached ones with the building cage, ground
    /// ones with the lot cage or the blend; props near a facade turn with it. SubObjectSystem places them from these
    /// poses, so changing the clone's data moves the instances.
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

            int deformedCount = 0;

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
                var mode = onGround ? PlacementMode.Upright : context.Options.AttachedPropMode;
                var pose = RigidDeformer.Place(DeformationOf(info, onGround, context), context.AlignmentBox,
                    context.Options.FacadeReachMeters, new RigidPose(info.m_Position, info.m_Rotation), mode);
                info.m_Position = pose.Position;
                info.m_Rotation = pose.Rotation;
                deformed[i] = info;
                deformedCount++;
            }
            subObjects.m_SubObjects = deformed;
            return new AttachmentCount("props", deformed.Length, deformedCount);
        }

        // Markers (spawn points, entrances) belong to doors; ground props over the footprint (steps) to the walls.
        private static IDeformation DeformationOf(ObjectSubObjectInfo info, bool onGround, AttachmentContext context)
        {
            if (!onGround)
                return context.Building;
            if (info.m_Object is MarkerObjectPrefab)
                return context.Paths;
            return context.IsInFootprint(info.m_Position) ? context.Ground : context.Lot;
        }
    }
}
