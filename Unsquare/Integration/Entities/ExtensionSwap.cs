using Unity.Entities;
using Unsquare.Core.Deformation;

namespace Unsquare.Integration.Entities
{
    /// <summary>
    /// An upgrade extension that changes prefab together with its building. <see cref="NetRemap"/> moves its
    /// lot nets from their current layout to the new one, in the building's space; null keeps them in place.
    /// </summary>
    internal readonly struct ExtensionSwap
    {
        public ExtensionSwap(Entity extension, Entity targetPrefab, LotRemap netRemap)
        {
            Extension = extension;
            TargetPrefab = targetPrefab;
            NetRemap = netRemap;
        }

        public Entity Extension { get; }
        public Entity TargetPrefab { get; }
        public LotRemap NetRemap { get; }
    }
}
