namespace Unsquare.Integration.PlacemakerCompatibility
{
    internal enum PlacemakerAdoptionStatus
    {
        /// <summary>The building is adopted: requesting <see cref="PlacemakerAdoption.Spec"/> takes it over.</summary>
        Ready,

        /// <summary>Placemaker is about to reshape the building; asking again a moment later works.</summary>
        PlacemakerBusy,

        /// <summary>Placemaker is not loaded or its shape system changed.</summary>
        PlacemakerMissing,

        /// <summary>Not a Placemaker building, no base prefab or no lot.</summary>
        Failed,
    }
}
