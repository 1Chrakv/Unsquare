using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Unity.Entities;

namespace Unsquare.Integration.Import
{
    /// <summary>
    /// Reads and clears FlexIt's per-building shear through its public registry system, found by reflection
    /// (no compile-time reference). Every call degrades to "nothing" if FlexIt changed or is missing.
    /// </summary>
    internal sealed class FlexItBridge
    {
        // Release FlexIt and a local rebuild of it may be loaded side by side; each keeps its own skews.
        private static readonly string[] AssemblyNames = { "BuildingSkew", "FlexItDecompiled" };
        private const string RegistryTypeName = "SkewRegistrySystem";

        private readonly List<Registry> _registries;

        private FlexItBridge(List<Registry> registries)
        {
            _registries = registries;
        }

        /// <summary>False if FlexIt is not loaded or no registry has the expected members.</summary>
        public static bool TryFind(World world, out FlexItBridge bridge)
        {
            var registries = new List<Registry>();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                string name = assembly.GetName().Name;
                if (Array.IndexOf(AssemblyNames, name) < 0)
                    continue;
                try
                {
                    if (Registry.TryCreate(world, assembly.GetType($"{name}.{RegistryTypeName}", false), out var registry))
                        registries.Add(registry);
                    else
                        Mod.Log.Warn($"{name} is loaded, but its skew registry looks different than expected.");
                }
                catch (Exception e)
                {
                    Mod.Log.Error(e, $"Looking up the skew registry of {name} failed.");
                }
            }
            bridge = registries.Count > 0 ? new FlexItBridge(registries) : null;
            return bridge != null;
        }

        /// <summary>Snapshot of every building's shear (degrees, lean-front flag) across all registries.</summary>
        public Dictionary<Entity, (float Degrees, bool LeanFront)> ReadSkews()
        {
            var result = new Dictionary<Entity, (float, bool)>();
            foreach (var registry in _registries)
                registry.ReadInto(result);
            return result;
        }

        /// <summary>FlexIt's own removal also redraws the building and updates its skews.json.</summary>
        public bool TryRemoveSkew(Entity building)
        {
            bool removed = true;
            foreach (var registry in _registries)
                removed &= registry.TryRemove(building);
            return removed;
        }

        private sealed class Registry
        {
            private readonly object _system;
            private readonly PropertyInfo _skewed;
            private readonly MethodInfo _removeSkew;

            private Registry(object system, PropertyInfo skewed, MethodInfo removeSkew)
            {
                _system = system;
                _skewed = skewed;
                _removeSkew = removeSkew;
            }

            private string Name => _system.GetType().Assembly.GetName().Name;

            public static bool TryCreate(World world, Type type, out Registry registry)
            {
                registry = null;
                if (type == null)
                    return false;
                var system = world.GetExistingSystemManaged(type);
                var skewed = type.GetProperty("Skewed", BindingFlags.Instance | BindingFlags.Public);
                var removeSkew = type.GetMethod("RemoveSkew", BindingFlags.Instance | BindingFlags.Public, null,
                    new[] { typeof(Entity) }, null);
                if (system == null || skewed == null || removeSkew == null)
                    return false;
                registry = new Registry(system, skewed, removeSkew);
                return true;
            }

            public void ReadInto(Dictionary<Entity, (float, bool)> result)
            {
                int count = 0;
                try
                {
                    if (!(_skewed.GetValue(_system) is IDictionary skews))
                        return;
                    FieldInfo degrees = null, leanFront = null;
                    foreach (DictionaryEntry pair in skews)
                    {
                        if (!(pair.Key is Entity entity) || pair.Value == null)
                            continue;
                        var entryType = pair.Value.GetType();
                        degrees ??= entryType.GetField("Degrees");
                        leanFront ??= entryType.GetField("LeanFront");
                        if (degrees == null || leanFront == null)
                        {
                            Mod.Log.Warn($"Skew entries of {Name} look different than expected.");
                            return;
                        }
                        result[entity] = ((float)degrees.GetValue(pair.Value), (bool)leanFront.GetValue(pair.Value));
                        count++;
                    }
                }
                catch (Exception e)
                {
                    Mod.Log.Error(e, $"Reading skews of {Name} failed.");
                }
                finally
                {
                    Mod.Log.Info($"FlexIt import: {count} skews in {Name}.");
                }
            }

            public bool TryRemove(Entity building)
            {
                try
                {
                    _removeSkew.Invoke(_system, new object[] { building });
                    return true;
                }
                catch (Exception e)
                {
                    Mod.Log.Error(e, $"Removing the {Name} skew of building {building} failed.");
                    return false;
                }
            }
        }
    }
}
