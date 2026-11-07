using Unsquare.Core.Cage;

namespace Unsquare.Core.Deformation
{
    /// <summary>
    /// The deformations of a spec: building cage, lot cage, their blend around the building's footprint, and which of
    /// them paths and ground items follow (<see cref="BlendMode"/>). Linked specs share one lot-cage deformation.
    /// </summary>
    public sealed class SplitDeformations
    {
        private SplitDeformations(IDeformation building, IDeformation lot, IDeformation blend, IDeformation paths,
            IDeformation ground, CageBox footprint)
        {
            Building = building;
            Lot = lot;
            Blend = blend;
            Paths = paths;
            Ground = ground;
            Footprint = footprint;
        }

        public IDeformation Building { get; }
        public IDeformation Lot { get; }

        /// <summary>The building's deformation inside the footprint, the lot's beyond the blend ring.</summary>
        public IDeformation Blend { get; }

        /// <summary>What paths, lanes and markers follow: the blend unless the mode is <see cref="BlendMode.None"/>.</summary>
        public IDeformation Paths { get; }

        /// <summary>What ground props over the footprint and ground areas follow: the blend only for All.</summary>
        public IDeformation Ground { get; }

        /// <summary>The building's mesh box (its footprint in xz).</summary>
        public CageBox Footprint { get; }

        public static SplitDeformations Create(CageBox lotBox, CageBox buildingBox, DeformSpec spec)
        {
            var lot = new TrilinearCageDeformation(lotBox, spec.LotOffsets);
            if (spec.IsLinked)
                return new SplitDeformations(lot, lot, lot, lot, lot, buildingBox);
            var building = new TrilinearCageDeformation(lotBox, spec.BuildingOffsets);
            var blend = new BlendedDeformation(building, lot, buildingBox, CageBlend.Ring(lotBox, buildingBox, spec));
            return new SplitDeformations(building, lot, blend,
                spec.BlendMode == BlendMode.None ? lot : blend,
                spec.BlendMode == BlendMode.All ? blend : lot,
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
