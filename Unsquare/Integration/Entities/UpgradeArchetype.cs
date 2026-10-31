using System;
using System.Collections.Generic;
using System.Reflection;
using Colossal.Entities;
using Game.Prefabs;
using Unity.Collections;
using Unity.Entities;
using InstalledUpgrade = Game.Buildings.InstalledUpgrade;
using NetSubNet = Game.Net.SubNet;
using SubRoute = Game.Routes.SubRoute;

namespace Unsquare.Integration.Entities
{
    /// <summary>
    /// A building prefab gets upgrade buffers in its instance archetype only when upgrades register with it, which
    /// they never do with a variant. The tool's temporary copies of a variant then lack <see cref="InstalledUpgrade"/>,
    /// and the game's upgrade reference job crashes on them; this adds the buffers the base prefab's archetype has.
    /// </summary>
    internal static class UpgradeArchetype
    {
        /// <returns>True if the variant's archetype was extended.</returns>
        public static bool AddUpgradeBuffers(EntityManager entityManager, Entity basePrefab, Entity variantPrefab)
        {
            if (!entityManager.HasBuffer<BuildingUpgradeElement>(basePrefab) ||
                !entityManager.TryGetComponent(variantPrefab, out ObjectData data) || !data.m_Archetype.Valid)
                return false;

            // The same types BuildingPrefab.RefreshArchetype adds for prefabs with upgrades.
            var required = new[]
            {
                ComponentType.ReadWrite<InstalledUpgrade>(), ComponentType.ReadWrite<NetSubNet>(),
                ComponentType.ReadWrite<SubRoute>(),
            };
            var types = data.m_Archetype.GetComponentTypes(Allocator.Temp);
            var combined = new List<ComponentType>(types.Length + required.Length);
            try
            {
                combined.AddRange(types);
                foreach (var type in required)
                {
                    if (!Contains(types, type))
                        combined.Add(type);
                }
            }
            finally
            {
                types.Dispose();
            }
            if (combined.Count == types.Length)
                return false;
            try
            {
                if (CreateArchetypeMethod == null)
                    throw new MissingMethodException(nameof(EntityManager), nameof(EntityManager.CreateArchetype));
                var archetype = (EntityArchetype)CreateArchetypeMethod.Invoke(entityManager,
                    new object[] { combined.ToArray() });
                entityManager.SetComponentData(variantPrefab, new ObjectData { m_Archetype = archetype });
                return true;
            }
            catch (Exception e)
            {
                // Without the buffers, hovering the building with an upgrade can crash the game.
                Mod.Log.Error(e, $"Adding upgrade buffers to variant prefab {variantPrefab} failed.");
                return false;
            }
        }

        // Called by reflection: a direct call also binds the ReadOnlySpan overload, which the game's mscorlib lacks.
        private static readonly MethodInfo CreateArchetypeMethod = typeof(EntityManager).GetMethod(
            nameof(EntityManager.CreateArchetype), BindingFlags.Instance | BindingFlags.Public, null,
            new[] { typeof(ComponentType[]) }, null);

        private static bool Contains(NativeArray<ComponentType> types, ComponentType type)
        {
            foreach (var candidate in types)
            {
                if (candidate.TypeIndex == type.TypeIndex)
                    return true;
            }
            return false;
        }
    }
}
