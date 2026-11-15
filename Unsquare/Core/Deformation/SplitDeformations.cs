using Unsquare.Core.Cage;

namespace Unsquare.Core.Deformation
{
    /// <summary>
    /// The deformations of a spec: building cage, lot cage, their blend around the building's footprint, and which of
    /// them ground items next to the building follow (<see cref="BlendMode"/>). Linked specs share one deformation.
    /// </summary>
    public sealed class SplitDeformations
    {
        private SplitDeformations(IDeformation building, IDeformation lot, IDeformation blend, IDeformation ground,
            CageBox footprint)
        {
            Building = building;
            Lot = lot;
            Blend = blend;
            Ground = ground;
            Footprint = footprint;
        }

        public IDeformation Building { get; }
        public IDeformation Lot { get; }

        /// <summary>The building's deformation inside the footprint, the lot's beyond the blend ring.</summary>
        public IDeformation Blend { get; }

        /// <summary>
        /// What everything on the ground (paths, lanes, props, markers, areas) follows: the blend for
        /// <see cref="BlendMode.All"/>, the lot cage for <see cref="BlendMode.None"/>.
        /// </summary>
        public IDeformation Ground { get; }

        /// <summary>The building's mesh box (its footprint in xz).</summary>
        public CageBox Footprint { get; }

        public static SplitDeformations Create(CageBox lotBox, CageBox buildingBox, DeformSpec spec)
        {
            var lot = new TrilinearCageDeformation(lotBox, spec.LotOffsets);
            if (spec.IsLinked)
                return new SplitDeformations(lot, lot, lot, lot, buildingBox);
            var building = new TrilinearCageDeformation(lotBox, spec.BuildingOffsets);
            var blend = new BlendedDeformation(building, lot, buildingBox, CageBlend.Ring(lotBox, buildingBox, spec));
            return new SplitDeformations(building, lot, blend, spec.BlendMode == BlendMode.All ? blend : lot,
                buildingBox);
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
