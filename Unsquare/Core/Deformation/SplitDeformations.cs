using Unsquare.Core.Cage;

namespace Unsquare.Core.Deformation
{
    /// <summary>
    /// The three deformations of a spec: building cage, lot cage and their blend around the building's footprint.
    /// Linked specs give one shared lot-cage deformation for all three, exactly as before the split.
    /// </summary>
    public sealed class SplitDeformations
    {
        private SplitDeformations(IDeformation building, IDeformation lot, IDeformation blend, CageBox footprint)
        {
            Building = building;
            Lot = lot;
            Blend = blend;
            Footprint = footprint;
        }

        public IDeformation Building { get; }
        public IDeformation Lot { get; }

        /// <summary>The building's deformation inside the footprint, the lot's beyond the blend ring.</summary>
        public IDeformation Blend { get; }

        /// <summary>The building's mesh box (its footprint in xz).</summary>
        public CageBox Footprint { get; }

        public static SplitDeformations Create(CageBox lotBox, CageBox buildingBox, DeformSpec spec)
        {
            var lot = new TrilinearCageDeformation(lotBox, spec.LotOffsets);
            if (spec.IsLinked)
                return new SplitDeformations(lot, lot, lot, buildingBox);
            var building = new TrilinearCageDeformation(lotBox, spec.BuildingOffsets);
            var blend = new BlendedDeformation(building, lot, buildingBox, CageBlend.Ring(lotBox, buildingBox, spec));
            return new SplitDeformations(building, lot, blend, buildingBox);
        }

        /// <summary>
        /// Twist of the building cage on the box it bends (see <see cref="CageTwist"/>): measured on the building box
        /// when split, where lot-box offsets would amplify it.
        /// </summary>
        public static float BuildingTwistMeters(CageBox lotBox, CageBox buildingBox, DeformSpec spec) =>
            CageTwist.MaxResidualMeters(spec.IsLinked
                ? spec.LotOffsets
                : ExtendedCage.Convert(lotBox, spec.BuildingOffsets, buildingBox));
    }
}
