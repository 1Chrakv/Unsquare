using Game.Prefabs;
using Unsquare.Core.Cage;

namespace Unsquare.Integration.Systems
{
    /// <summary>A building's base prefab, its undeformed cage boxes per scope and the shape currently applied.</summary>
    internal readonly struct BuildingDescription
    {
        private readonly CageBox _lotBox;

        public BuildingDescription(ObjectGeometryPrefab basePrefab, CageBox buildingBox, CageBox? lotBox,
            CageShape applied)
        {
            BasePrefab = basePrefab;
            BuildingBox = buildingBox;
            SupportsLot = lotBox.HasValue;
            _lotBox = lotBox ?? buildingBox;
            Applied = applied;
        }

        public ObjectGeometryPrefab BasePrefab { get; }
        public CageBox BuildingBox { get; }
        public bool SupportsLot { get; }
        public CageShape Applied { get; }

        /// <summary>Lot scope falls back to Building for prefabs without a lot (e.g. non-building objects).</summary>
        public CageScope Effective(CageScope requested) =>
            requested == CageScope.Lot && SupportsLot ? CageScope.Lot : CageScope.Building;

        public CageBox BoxFor(CageScope scope) => Effective(scope) == CageScope.Lot ? _lotBox : BuildingBox;
    }
}
