using Game.Prefabs;
using Unsquare.Core.Cage;

namespace Unsquare.Integration.Systems
{
    /// <summary>A base prefab's lot box and building box; false for prefabs without a lot.</summary>
    internal delegate bool LotBoxReader(ObjectGeometryPrefab basePrefab, out CageBox lotBox, out CageBox buildingBox);
}
