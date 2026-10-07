using System.Runtime.CompilerServices;
using Unity.Entities;
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
            return Services.GetValue(system, s => new DeformService(s.Pipeline));
        }
    }
}
