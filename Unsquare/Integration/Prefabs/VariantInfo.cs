using Game.Prefabs;
using Unsquare.Core.Cage;

namespace Unsquare.Integration.Prefabs
{
    /// <summary>What a variant prefab was made from.</summary>
    internal sealed class VariantInfo
    {
        public VariantInfo(ObjectGeometryPrefab basePrefab, CageOffsets offsets)
        {
            BasePrefab = basePrefab;
            Offsets = offsets;
        }

        public ObjectGeometryPrefab BasePrefab { get; }
        public CageOffsets Offsets { get; }
    }
}
