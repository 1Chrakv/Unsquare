using Unity.Mathematics;

namespace Unsquare.Core.Cage
{
    /// <summary>
    /// Which of its owner's cages an upgrade extension's meshes follow. It decides the baked shape behind an extension
    /// variant's name, so the rule is part of the naming scheme: never change it.
    /// </summary>
    public static class ExtensionCage
    {
        /// <summary>
        /// Storeys and wings stand on the building (mesh centre over its footprint) and follow the building cage;
        /// extensions beside it, such as a car park, follow the lot cage.
        /// </summary>
        public static bool FollowsBuilding(CageBox extensionMeshBox, float3 extensionPosition, CageBox ownerBuildingBox)
        {
            float2 centre = extensionMeshBox.Center.xz + extensionPosition.xz;
            return math.all(centre >= ownerBuildingBox.Min.xz) && math.all(centre <= ownerBuildingBox.Max.xz);
        }
    }
}
