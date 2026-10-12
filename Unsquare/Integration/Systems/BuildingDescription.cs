using Game.Prefabs;
using Unsquare.Core.Cage;

namespace Unsquare.Integration.Systems
{
    /// <summary>A building's base prefab, its undeformed lot cage and mesh box, and the deformation currently applied.</summary>
    internal readonly struct BuildingDescription
    {
        public BuildingDescription(ObjectGeometryPrefab basePrefab, CageBox lotBox, CageBox buildingBox,
            DeformSpec applied)
        {
            BasePrefab = basePrefab;
            LotBox = lotBox;
            BuildingBox = buildingBox;
            Applied = applied;
        }

        public ObjectGeometryPrefab BasePrefab { get; }

        /// <summary>The cage box: lot rectangle from the ground up to the roof.</summary>
        public CageBox LotBox { get; }

        /// <summary>Render mesh bounds; props align to its faces.</summary>
        public CageBox BuildingBox { get; }

        public DeformSpec Applied { get; }
    }
}
