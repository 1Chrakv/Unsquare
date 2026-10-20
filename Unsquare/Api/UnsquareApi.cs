using System.Runtime.CompilerServices;
using Unity.Entities;
using Unsquare.Integration.Entities;
using Unsquare.Integration.Systems;

namespace Unsquare.Api
{
    /// <summary>Hands out the deform service of a world, e.g. from a UI system's OnCreate.</summary>
    public static class UnsquareApi
    {
        private static readonly ConditionalWeakTable<VariantSystem, DeformService> Services = new();

        public static IBuildingDeformService GetDeformService(World world)
        {
            var system = world.GetOrCreateSystemManaged<VariantSystem>();
            var terrain = world.GetOrCreateSystemManaged<BaseTerrainSystem>();
            var neighbours = new NeighbourQuery(world.EntityManager,
                world.GetOrCreateSystemManaged<Game.Net.SearchSystem>(),
                world.GetOrCreateSystemManaged<Game.Objects.SearchSystem>());
            return Services.GetValue(system, s => new DeformService(s.Pipeline, terrain, neighbours, world.EntityManager));
        }
    }
}
