using Game.Prefabs;
using Unsquare.Core.Deformation;

namespace Unsquare.Integration.Prefabs.Attachments
{
    /// <summary>
    /// Moves and rotates the building's props (sub-objects) rigidly with the cage; props near a facade turn with
    /// it. SubObjectSystem places them from these poses, so changing the clone's data moves the instances.
    /// </summary>
    internal sealed class SubObjectDeformer : IAttachmentDeformer
    {
        /// <summary>Sub-objects without a parent mesh stand on the ground (trees, sheds) and snap to terrain.</summary>
        private const int GroundParentMesh = -1;

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
                if (original == null || !context.Affects(original.m_Position))
                {
                    deformed[i] = original;
                    continue;
                }
                var info = PrefabCloning.ShallowCopy(original);
                var mode = info.m_ParentMesh == GroundParentMesh ? PlacementMode.Upright : PlacementMode.Follow;
                var pose = RigidDeformer.Place(context.Deformation, context.AlignmentBox, context.Options.FacadeReachMeters,
                    new RigidPose(info.m_Position, info.m_Rotation), mode);
                info.m_Position = pose.Position;
                info.m_Rotation = pose.Rotation;
                deformed[i] = info;
                deformedCount++;
            }
            subObjects.m_SubObjects = deformed;
            return new AttachmentCount("props", deformed.Length, deformedCount);
        }
    }
}
