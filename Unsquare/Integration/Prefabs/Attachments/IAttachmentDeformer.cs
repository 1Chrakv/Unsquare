using Game.Prefabs;
using Unsquare.Core.Deformation;

namespace Unsquare.Integration.Prefabs.Attachments
{
    /// <summary>
    /// Deforms one kind of building-relative placement on a variant clone: props, areas, lanes, nets.
    /// Planned: effects (EffectSource).
    /// </summary>
    internal interface IAttachmentDeformer
    {
        /// <summary>What this deformer handles; disabled kinds (<see cref="AttachmentOptions"/>) are skipped.</summary>
        AttachmentKind Kind { get; }

        /// <summary>
        /// Deforms all attachments of its kind with the lot cage; must detach any data it changes from
        /// <paramref name="source"/> so the base prefab stays intact.
        /// </summary>
        AttachmentCount Apply(ObjectGeometryPrefab clone, ObjectGeometryPrefab source, AttachmentContext context);
    }
}
