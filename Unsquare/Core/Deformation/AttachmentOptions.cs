using System;

namespace Unsquare.Core.Deformation
{
    /// <summary>
    /// Which attachments follow the cage and how props are placed. Read when a variant is created, so changes
    /// apply to new deformations.
    /// </summary>
    public sealed class AttachmentOptions
    {
        public static readonly AttachmentOptions Default = new(true, true, true, true, PlacementMode.Follow,
            FaceAlignment.DefaultReachMeters);

        public AttachmentOptions(bool deformProps, bool deformAreas, bool deformLanes, bool deformNets,
            PlacementMode attachedPropMode, float facadeReachMeters)
        {
            if (!(facadeReachMeters >= 0f))
                throw new ArgumentException("Facade reach must not be negative.", nameof(facadeReachMeters));
            DeformProps = deformProps;
            DeformAreas = deformAreas;
            DeformLanes = deformLanes;
            DeformNets = deformNets;
            AttachedPropMode = attachedPropMode;
            FacadeReachMeters = facadeReachMeters;
        }

        public bool DeformProps { get; }
        public bool DeformAreas { get; }
        public bool DeformLanes { get; }
        public bool DeformNets { get; }

        /// <summary>Rotation of props attached to the building; ground props always stay upright.</summary>
        public PlacementMode AttachedPropMode { get; }

        /// <summary>Distance from a facade within which props turn with it; 0 disables facade alignment.</summary>
        public float FacadeReachMeters { get; }

        public bool IsEnabled(AttachmentKind kind) => kind switch
        {
            AttachmentKind.Props => DeformProps,
            AttachmentKind.Areas => DeformAreas,
            AttachmentKind.Lanes => DeformLanes,
            AttachmentKind.Nets => DeformNets,
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

        public override string ToString() =>
            $"props {DeformProps}, areas {DeformAreas}, lanes {DeformLanes}, nets {DeformNets}, " +
            $"attached {AttachedPropMode}, facade reach {FacadeReachMeters} m";
    }
}
