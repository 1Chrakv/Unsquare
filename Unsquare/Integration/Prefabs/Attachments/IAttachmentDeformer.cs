using Game.Prefabs;
using Unsquare.Core.Deformation;

namespace Unsquare.Integration.Prefabs.Attachments
{
    /// <summary>
    /// Deforms one kind of building-relative placement on a variant clone. Implemented: sub-objects (props).
    /// Planned: effects (EffectSource), lot areas (ObjectSubAreas), paths (ObjectSubNets), lanes (ObjectSubLanes).
    /// </summary>
    internal interface IAttachmentDeformer
    {
        /// <summary>
        /// Deforms what <see cref="AttachmentContext.Affects"/> selects; must detach any data it changes from
        /// <paramref name="source"/> so the base prefab stays intact.
        /// </summary>
        void Apply(ObjectGeometryPrefab clone, ObjectGeometryPrefab source, AttachmentContext context);
    }
}
