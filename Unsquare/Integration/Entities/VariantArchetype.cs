using System;
using System.Collections.Generic;
using System.Reflection;
using Colossal.Entities;
using Game.Prefabs;
using Unity.Collections;
using Unity.Entities;

namespace Unsquare.Integration.Entities
{
    /// <summary>
    /// A variant's instance archetype lacks what its base gets from SpawnableBuilding and from upgrades registering
    /// with it. Copies of a variant (eyedropper, hover) then miss components the game's jobs read unchecked, and the
    /// game crashes natively; this gives the variant's archetype every type the base's archetype has.
    /// </summary>
    internal static class VariantArchetype
    {
        /// <summary>Adds the base archetype's types the variant lacks and lists them in <paramref name="added"/>.</summary>
        /// <returns>True if the variant's archetype was extended.</returns>
        public static bool TryComplete(EntityManager entityManager, Entity basePrefab, Entity variantPrefab,
            List<ComponentType> added)
        {
            added.Clear();
            if (!entityManager.TryGetComponent(basePrefab, out ObjectData baseData) || !baseData.m_Archetype.Valid ||
                !entityManager.TryGetComponent(variantPrefab, out ObjectData data) || !data.m_Archetype.Valid)
            {
                // An uninitialized variant: buildings swapped to it crash the game (VariantSystem refuses such swaps).
                Mod.Log.Warn($"Variant prefab {variantPrefab} or its base {basePrefab} has no instance archetype.");
                return false;
            }

            var baseTypes = baseData.m_Archetype.GetComponentTypes(Allocator.Temp);
            var types = data.m_Archetype.GetComponentTypes(Allocator.Temp);
            var combined = new List<ComponentType>(baseTypes.Length + types.Length);
            try
            {
                combined.AddRange(types);
                foreach (var type in baseTypes)
                {
                    if (!Contains(types, type))
                        added.Add(type);
                }
                combined.AddRange(added);
            }
            finally
            {
                baseTypes.Dispose();
                types.Dispose();
            }
            if (added.Count == 0)
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
                // Without the missing types, hovering or clicking a copy of the variant can crash the game.
                Mod.Log.Error(e, $"Completing the archetype of variant prefab {variantPrefab} failed.");
                added.Clear();
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
