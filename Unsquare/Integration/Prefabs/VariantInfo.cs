using Game.Prefabs;
using Unsquare.Core.Cage;

namespace Unsquare.Integration.Prefabs
{
    /// <summary>What a variant prefab was made from.</summary>
    internal sealed class VariantInfo
    {
        public VariantInfo(ObjectGeometryPrefab basePrefab, DeformSpec spec)
        {
            BasePrefab = basePrefab;
            Spec = spec;
        }

        public ObjectGeometryPrefab BasePrefab { get; }
        public DeformSpec Spec { get; }
    }
}
