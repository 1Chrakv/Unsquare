using Game.Prefabs;
using Unsquare.Core.Cage;

namespace Unsquare.Integration.Systems
{
    /// <summary>A building's base prefab, its undeformed cage box and the offsets currently applied.</summary>
    internal readonly struct BuildingDescription
    {
        public BuildingDescription(ObjectGeometryPrefab basePrefab, CageBox box, CageOffsets appliedOffsets)
        {
            BasePrefab = basePrefab;
            Box = box;
            AppliedOffsets = appliedOffsets;
        }

        public ObjectGeometryPrefab BasePrefab { get; }
        public CageBox Box { get; }
        public CageOffsets AppliedOffsets { get; }
    }
}
