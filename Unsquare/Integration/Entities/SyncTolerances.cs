namespace Unsquare.Integration.Entities
{
    /// <summary>
    /// How far saved lot areas and nets may be from their expected layout (mean, meters) and still be matched;
    /// anything further off was edited by the player and stays put.
    /// </summary>
    internal readonly struct SyncTolerances
    {
        public SyncTolerances(float areaMeters, float netMeters)
        {
            AreaMeters = areaMeters;
            NetMeters = netMeters;
        }

        public float AreaMeters { get; }
        public float NetMeters { get; }
    }
}
