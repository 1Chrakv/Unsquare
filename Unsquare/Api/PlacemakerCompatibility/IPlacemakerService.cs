using Unity.Entities;

namespace Unsquare.Api.PlacemakerCompatibility
{
    /// <summary>
    /// Takes buildings Placemaker has bent over as Unsquare cages of the same shape. Until then Unsquare does not edit
    /// them. Get it with <see cref="UnsquareApi.GetPlacemakerService"/>.
    /// </summary>
    public interface IPlacemakerService
    {
        /// <summary>True for a building on a Placemaker variant the player has not converted: it can't be edited yet.</summary>
        bool NeedsConversion(Entity building);

        /// <summary>
        /// Gives a Placemaker building an Unsquare cage of the same shape on its plain prefab; Placemaker then leaves
        /// it alone. Not undoable; a reset returns the plain building.
        /// </summary>
        PlacemakerConversionResult Convert(Entity building);

        /// <summary>
        /// How many houses <see cref="ConvertRow"/> would convert: the building and every undecided house Placemaker
        /// joined to it along its row. 0 or 1 when there is no row.
        /// </summary>
        int RowSize(Entity building);

        /// <summary>
        /// Converts the building and the rest of its Placemaker row at once; their swaps land in the same frame, so
        /// Placemaker can't straighten the neighbours of a house that left its plan. Returns the building's result;
        /// refused as a whole while Placemaker reshapes any of them.
        /// </summary>
        PlacemakerConversionResult ConvertRow(Entity building);
    }
}
