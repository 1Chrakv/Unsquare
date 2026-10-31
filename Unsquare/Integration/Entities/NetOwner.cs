using Unity.Entities;
using Unsquare.Core.Deformation;

namespace Unsquare.Integration.Entities
{
    /// <summary>An entity whose sub-nets are synced, with the remap for them; null keeps them in place.</summary>
    internal readonly struct NetOwner
    {
        public NetOwner(Entity owner, LotRemap remap)
        {
            Owner = owner;
            Remap = remap;
        }

        public Entity Owner { get; }
        public LotRemap Remap { get; }
    }
}
