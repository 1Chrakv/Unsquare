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
        private const string AssemblyName = "BuildingSkew";
        private const string RegistryTypeName = "BuildingSkew.SkewRegistrySystem";

        private readonly object _registry;
        private readonly PropertyInfo _skewed;
        private readonly MethodInfo _removeSkew;

        private FlexItBridge(object registry, PropertyInfo skewed, MethodInfo removeSkew)
        {
            _registry = registry;
            _skewed = skewed;
            _removeSkew = removeSkew;
        }

        /// <summary>False if FlexIt is not loaded or its registry no longer has the expected members.</summary>
        public static bool TryFind(World world, out FlexItBridge bridge)
        {
            bridge = null;
            try
            {
                var type = FindRegistryType();
                if (type == null)
                    return false;
                var registry = world.GetExistingSystemManaged(type);
                var skewed = type.GetProperty("Skewed", BindingFlags.Instance | BindingFlags.Public);
                var removeSkew = type.GetMethod("RemoveSkew", BindingFlags.Instance | BindingFlags.Public, null,
                    new[] { typeof(Entity) }, null);
                if (registry == null || skewed == null || removeSkew == null)
                {
                    Mod.Log.Warn("FlexIt is loaded, but its skew registry looks different than expected.");
                    return false;
                }
                bridge = new FlexItBridge(registry, skewed, removeSkew);
                return true;
            }
            catch (Exception e)
            {
                Mod.Log.Error(e, "Looking up FlexIt failed.");
                return false;
            }
        }

        /// <summary>Snapshot of every building's shear: degrees and the lean-front flag.</summary>
        public Dictionary<Entity, (float Degrees, bool LeanFront)> ReadSkews()
        {
            var result = new Dictionary<Entity, (float, bool)>();
            try
            {
                if (!(_skewed.GetValue(_registry) is IDictionary skews))
                    return result;
                FieldInfo degrees = null, leanFront = null;
                foreach (DictionaryEntry pair in skews)
                {
                    if (!(pair.Key is Entity entity) || pair.Value == null)
                        continue;
                    var entryType = pair.Value.GetType();
                    degrees ??= entryType.GetField("Degrees");
                    leanFront ??= entryType.GetField("LeanFront");
                    if (degrees == null || leanFront == null)
                        break;
                    result[entity] = ((float)degrees.GetValue(pair.Value), (bool)leanFront.GetValue(pair.Value));
                }
            }
            catch (Exception e)
            {
                Mod.Log.Error(e, "Reading FlexIt skews failed.");
            }
            return result;
        }

        /// <summary>FlexIt's own removal also redraws the building and updates its skews.json.</summary>
        public bool TryRemoveSkew(Entity building)
        {
            try
            {
                _removeSkew.Invoke(_registry, new object[] { building });
                return true;
            }
            catch (Exception e)
            {
                Mod.Log.Error(e, $"Removing the FlexIt skew of building {building} failed.");
                return false;
            }
        }

        private static Type FindRegistryType()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.GetName().Name == AssemblyName)
                    return assembly.GetType(RegistryTypeName, false);
            }
            return null;
        }
    }
}
