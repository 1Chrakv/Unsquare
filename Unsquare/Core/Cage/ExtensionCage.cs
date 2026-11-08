using Unity.Mathematics;

namespace Unsquare.Core.Cage
{
    /// <summary>
    /// Which of its owner's cages an upgrade extension's meshes follow. It decides the baked shape behind an extension
    /// variant's name, so the rule is part of the naming scheme: never change it once released.
    /// </summary>
    public static class ExtensionCage
    {
        /// <summary>Extensions reaching this high (building space) are structures, not ground pieces.</summary>
        public const float StructureMinHeight = 4f;

        /// <summary>
        /// Storeys (mesh centre over the footprint) and wings (structures beside it) follow the building cage; flat
        /// pieces beside it, such as a surface car park or a ground helipad, follow the lot cage.
        /// </summary>
        public static bool FollowsBuilding(CageBox extensionMeshBox, float3 extensionPosition, CageBox ownerBuildingBox)
        {
            if (extensionMeshBox.Max.y + extensionPosition.y >= StructureMinHeight)
                return true;
            float2 centre = extensionMeshBox.Center.xz + extensionPosition.xz;
            return math.all(centre >= ownerBuildingBox.Min.xz) && math.all(centre <= ownerBuildingBox.Max.xz);
        }
    }
}
