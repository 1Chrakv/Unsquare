using System;
using Unsquare.Core.Cage;
using Unsquare.Core.Deformation;

namespace Unsquare.Core.Editing
{
    /// <summary>
    /// Edits of a building's two cages, all stored on the lot box. The building cage is shown on its own (smaller)
    /// box, the view box; edits there are converted as changes, never as absolute offsets, so rounding never drifts.
    /// </summary>
    public static class SplitCageEdit
    {
        /// <summary>Lot-box offsets seen on another box, e.g. the building cage on the building's box.</summary>
        public static CageOffsets View(CageBox lotBox, CageOffsets offsets, CageBox viewBox) =>
            ExtendedCage.Convert(lotBox, offsets, viewBox);

        /// <summary>
        /// Moves the building cage from <paramref name="startView"/> to <paramref name="newView"/> on the view box.
        /// Drags pass their start; absolute commands (fit, align, reset handle) pass the current view.
        /// </summary>
        public static DeformSpec EditBuilding(CageBox lotBox, CageBox viewBox, DeformSpec start, CageOffsets startView,
            CageOffsets newView)
        {
            var change = ExtendedCage.ConvertDelta(viewBox, newView.Minus(startView), lotBox);
            return Snap(lotBox, viewBox, start.WithBuildingOffsets(start.BuildingOffsets.Plus(change)));
        }

        /// <summary>Sets the lot cage; the building stays as it is.</summary>
        public static DeformSpec EditLot(CageBox lotBox, CageBox viewBox, DeformSpec start, CageOffsets newLot) =>
            Snap(lotBox, viewBox, start.WithLotOffsets(newLot));

        /// <summary>Sets the lot cage and adds the same change to the building cage, which keeps its own shape.</summary>
        public static DeformSpec EditBoth(CageBox lotBox, CageBox viewBox, DeformSpec start, CageOffsets newLot)
        {
            var building = start.BuildingOffsets.Plus(newLot.Minus(start.LotOffsets));
            return Snap(lotBox, viewBox, new DeformSpec(newLot, building, start.FlatTerrain));
        }

        /// <summary>Resets the target's cage to its original shape; the other cage stays.</summary>
        public static DeformSpec Reset(CageBox lotBox, CageBox viewBox, DeformSpec spec, CageTarget target) =>
            target switch
            {
                CageTarget.Building => Snap(lotBox, viewBox, spec.WithBuildingOffsets(CageOffsets.Identity)),
                CageTarget.Lot => Snap(lotBox, viewBox, spec.WithLotOffsets(CageOffsets.Identity)),
                CageTarget.Both => DeformSpec.Identity,
                _ => throw new ArgumentOutOfRangeException(nameof(target)),
            };

        /// <summary>Makes the building follow the lot again, dropping the building's own shape.</summary>
        public static DeformSpec Link(DeformSpec spec) => spec.WithBuildingOffsets(spec.LotOffsets).Normalized();

        /// <summary>
        /// Makes the lot follow the building again: the lot takes the building cage's shape, continued over the lot
        /// box, as the building already does inside it.
        /// </summary>
        public static DeformSpec LinkLot(DeformSpec spec) => spec.WithLotOffsets(spec.BuildingOffsets).Normalized();

        /// <summary>
        /// Removes differences that only rounding made: a building view equal to the lot's links the cages, an
        /// undeformed building view resets the building cage.
        /// </summary>
        public static DeformSpec Snap(CageBox lotBox, CageBox viewBox, DeformSpec spec)
        {
            if (spec.IsLinked)
                return spec.Normalized();
            var view = View(lotBox, spec.BuildingOffsets, viewBox);
            if (view.Equals(View(lotBox, spec.LotOffsets, viewBox)))
                return Link(spec);
            return view.IsIdentity ? spec.WithBuildingOffsets(CageOffsets.Identity).Normalized() : spec.Normalized();
        }

        /// <summary>
        /// Clamps the building cage to the limits of the building's box (not of the larger lot box, where its offsets
        /// are amplified). Linked cages are clamped with the lot.
        /// </summary>
        public static DeformSpec ClampBuilding(CageBox lotBox, CageBox buildingBox, DeformSpec spec, CageLimits limits)
        {
            if (spec.IsLinked)
                return spec;
            var view = View(lotBox, spec.BuildingOffsets, buildingBox);
            var clamped = CageValidator.Clamp(buildingBox, view, limits);
            if (clamped.Equals(view))
                return spec;
            var change = ExtendedCage.ConvertDelta(buildingBox, clamped.Minus(view), lotBox);
            return spec.WithBuildingOffsets(spec.BuildingOffsets.Plus(change));
        }
    }
}
