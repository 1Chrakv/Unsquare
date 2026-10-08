using Game.Prefabs;
using Unsquare.Core.Deformation;

namespace Unsquare.Integration.Prefabs.Attachments
{
    /// <summary>
    /// Moves and rotates the building's props (sub-objects) rigidly with the cage. SubObjectSystem places them
    /// from these building-space poses, so changing the clone's data moves the instances after the swap.
    /// </summary>
    internal sealed class SubObjectDeformer : IAttachmentDeformer
    {
        public void Apply(ObjectGeometryPrefab clone, ObjectGeometryPrefab source, IDeformation deformation)
        {
            var subObjects = PrefabCloning.GetOwnedComponent<ObjectSubObjects>(clone, source);
            if (subObjects == null || subObjects.m_SubObjects == null)
                return;

            // New array of copies: the infos may still be shared with the base prefab.
            var deformed = new ObjectSubObjectInfo[subObjects.m_SubObjects.Length];
            for (int i = 0; i < deformed.Length; i++)
            {
                var original = subObjects.m_SubObjects[i];
                if (original == null)
                    continue;
                var info = PrefabCloning.ShallowCopy(original);
                var pose = RigidDeformer.Deform(deformation, new RigidPose(info.m_Position, info.m_Rotation));
                info.m_Position = pose.Position;
                info.m_Rotation = pose.Rotation;
                deformed[i] = info;
            }
            subObjects.m_SubObjects = deformed;
        }
    }
}
