using Game.Prefabs;
using Unsquare.Core.Cage;

namespace Unsquare.Integration.Systems
{
    /// <summary>
    /// A building's base prefab, its undeformed lot cage, edit boxes and mesh box, and the deformation currently
    /// applied.
    /// </summary>
    internal readonly struct BuildingDescription
    {
        public BuildingDescription(ObjectGeometryPrefab basePrefab, CageBox lotBox, CageBox editBox,
            CageBox buildingBox, CageBox buildingEditBox, bool hasOwnBuildingBox, DeformSpec applied)
        {
            BasePrefab = basePrefab;
            LotBox = lotBox;
            EditBox = editBox;
            BuildingBox = buildingBox;
            BuildingEditBox = buildingEditBox;
            HasOwnBuildingBox = hasOwnBuildingBox;
            Applied = applied;
        }

        public ObjectGeometryPrefab BasePrefab { get; }

        /// <summary>The cage box: lot rectangle from the ground up to the roof. Both cages are stored on it.</summary>
        public CageBox LotBox { get; }

        /// <summary>
        /// The cage shown and edited when both cages move together: the lot box raised to the top of the deformed
        /// on-lot extensions (e.g. a storey). See <see cref="ExtendedCage"/>.
        /// </summary>
        public CageBox EditBox { get; }

        /// <summary>Render mesh bounds; props align to its faces and the blend centres on its footprint.</summary>
        public CageBox BuildingBox { get; }

        /// <summary>The building cage shown and edited on its own: the mesh box raised to deformed storeys.</summary>
        public CageBox BuildingEditBox { get; }

        /// <summary>False for flat or meshless lots (parks), whose building box falls back to the lot box.</summary>
        public bool HasOwnBuildingBox { get; }

        public DeformSpec Applied { get; }
    }
}
