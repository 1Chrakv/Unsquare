using Game.Prefabs;
using Unity.Entities;
using Unsquare.Core.Cage;

namespace Unsquare.Integration.Systems
{
    /// <summary>
    /// Buildings on another mod's variant prefabs (Placemaker's). <see cref="VariantSystem"/> asks before it resolves a
    /// base: such buildings are only described once they were taken over, on the plain base the other mod bent.
    /// </summary>
    internal interface IForeignPrefabs
    {
        /// <summary>True if the building's current prefab is a foreign variant, taken over or not.</summary>
        bool IsForeignPrefab(Entity building, PrefabBase current);

        /// <summary>The plain base a taken-over building is described on; false while it is undecided.</summary>
        bool TryGetTakenOverBase(Entity building, out ObjectGeometryPrefab basePrefab);

        /// <summary>For the hover preview of an undecided building: the base and the spec taking it over would give.</summary>
        bool TryGetPreview(Entity building, out ObjectGeometryPrefab basePrefab, out DeformSpec spec);

        /// <summary>True for a prefab the other mod creates at runtime, so it is missing when a save loads.</summary>
        bool IsRuntimePrefab(PrefabBase prefab);

        /// <summary>The building left its foreign prefab (swapped), or its takeover was cancelled before the swap.</summary>
        void Release(Entity building);

        /// <summary>Every takeover in flight was cancelled (reset of all buildings).</summary>
        void ReleaseAll();

        /// <summary>A new city is loading: forget everything.</summary>
        void Clear();

        /// <summary>The city is loaded and the simulation can run.</summary>
        void OnLoaded();
    }
}
