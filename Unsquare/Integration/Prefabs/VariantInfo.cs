using Game.Prefabs;
using Unsquare.Core.Cage;

namespace Unsquare.Integration.Prefabs
{
    /// <summary>What a variant prefab was made from.</summary>
    internal sealed class VariantInfo
    {
        public VariantInfo(ObjectGeometryPrefab basePrefab, CageShape shape)
        {
            BasePrefab = basePrefab;
            Shape = shape;
        }

        public ObjectGeometryPrefab BasePrefab { get; }
        public CageShape Shape { get; }
    }
}
