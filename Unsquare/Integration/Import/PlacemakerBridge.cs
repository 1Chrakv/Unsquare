using System;
using System.Reflection;
using Unity.Entities;
using Unity.Mathematics;
using Unsquare.Core.Import;

namespace Unsquare.Integration.Import
{
    /// <summary>
    /// Reads Placemaker's bent row houses through the public members of its shape system, found by reflection (no
    /// compile-time reference). Every call degrades to "nothing" if Placemaker changed or is missing.
    /// </summary>
    internal sealed class PlacemakerBridge
    {
        private const string AssemblyName = "Placemaker";
        private const string SystemTypeName = "Placemaker.Game.Shapes.ShapeSystem";

        private readonly object _system;
        private readonly MethodInfo _baseOf;
        private readonly MethodInfo _tryGetCage;
        private readonly MethodInfo _isRequested;
        private readonly Func<Entity, bool> _isVariant;
        private bool _loggedVariantFailure;

        private PlacemakerBridge(object system, Func<Entity, bool> isVariant, MethodInfo baseOf,
            MethodInfo tryGetCage, MethodInfo isRequested)
        {
            _system = system;
            _isVariant = isVariant;
            _baseOf = baseOf;
            _tryGetCage = tryGetCage;
            _isRequested = isRequested;
        }

        /// <summary>False if Placemaker is not loaded or its shape system looks different than expected.</summary>
        public static bool TryFind(World world, out PlacemakerBridge bridge)
        {
            bridge = null;
            try
            {
                Type type = null;
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (assembly.GetName().Name == AssemblyName)
                        type = assembly.GetType(SystemTypeName, false);
                    if (type != null)
                        break;
                }
                if (type == null)
                    return false;
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public;
                var system = type.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public)?.GetValue(null)
                             ?? world.GetExistingSystemManaged(type);
                var baseOf = type.GetMethod("BaseOf", flags, null, new[] { typeof(Entity) }, null);
                var isRequested = type.GetMethod("IsRequested", flags, null, new[] { typeof(Entity) }, null);
                var isVariant = type.GetMethod("IsVariant", flags, null, new[] { typeof(Entity) }, null);
                MethodInfo tryGetCage = null;
                foreach (var method in type.GetMethods(flags))
                {
                    var parameters = method.GetParameters();
                    if (method.Name == "TryGetCage" && method.ReturnType == typeof(bool) && parameters.Length == 2 &&
                        parameters[0].ParameterType == typeof(Entity) && parameters[1].IsOut)
                        tryGetCage = method;
                }
                if (system == null || baseOf == null || isRequested == null || tryGetCage == null ||
                    isVariant == null || isVariant.ReturnType != typeof(bool))
                {
                    Mod.Log.Warn("Placemaker is loaded, but its shape system looks different than expected.");
                    return false;
                }
                // Asked every frame for the hovered and edited building, so no reflection call per question.
                var ask = (Func<Entity, bool>)Delegate.CreateDelegate(typeof(Func<Entity, bool>), system, isVariant);
                bridge = new PlacemakerBridge(system, ask, baseOf, tryGetCage, isRequested);
                return true;
            }
            catch (Exception e)
            {
                Mod.Log.Error(e, "Looking up Placemaker's shape system failed.");
                return false;
            }
        }

        /// <summary>
        /// True for a prefab entity Placemaker bent. Its light copies carry the plain prefab's identity, so the
        /// prefab's name does not tell.
        /// </summary>
        public bool IsVariant(Entity prefab)
        {
            try
            {
                return _isVariant(prefab);
            }
            catch (Exception e)
            {
                if (!_loggedVariantFailure)
                    Mod.Log.Error(e, "Asking Placemaker about a prefab failed.");
                _loggedVariantFailure = true;
                return false;
            }
        }

        /// <summary>The plain prefab a Placemaker variant prefab was bent from.</summary>
        public bool TryGetBase(Entity variantPrefab, out Entity basePrefab)
        {
            basePrefab = Entity.Null;
            try
            {
                if (_baseOf.Invoke(_system, new object[] { variantPrefab }) is Entity found && found != variantPrefab)
                    basePrefab = found;
            }
            catch (Exception e)
            {
                Mod.Log.Error(e, $"Reading Placemaker's base of prefab {variantPrefab} failed.");
            }
            return basePrefab != Entity.Null;
        }

        /// <summary>The shape a Placemaker variant prefab was bent with, in building space.</summary>
        public bool TryGetCage(Entity variantPrefab, out PlacemakerCage cage)
        {
            cage = default;
            try
            {
                var args = new object[] { variantPrefab, null };
                if (!(bool)_tryGetCage.Invoke(_system, args) || args[1] == null)
                    return false;
                var shape = args[1];
                var type = shape.GetType();
                float2 Read(string name) => (float2)type.GetField(name, BindingFlags.Instance | BindingFlags.Public)
                    .GetValue(shape);
                cage = new PlacemakerCage(Read("Min"), Read("Max"), Read("BackLeft"), Read("BackRight"),
                    Read("FrontLeft"), Read("FrontRight"));
                return true;
            }
            catch (Exception e)
            {
                Mod.Log.Error(e, $"Reading Placemaker's shape of prefab {variantPrefab} failed.");
                return false;
            }
        }

        /// <summary>True while Placemaker is about to give the building another shape (asked, baking or swapping).</summary>
        public bool IsBusyWith(Entity building)
        {
            try
            {
                return (bool)_isRequested.Invoke(_system, new object[] { building });
            }
            catch (Exception e)
            {
                Mod.Log.Error(e, $"Asking Placemaker about building {building} failed.");
                return true;
            }
        }
    }
}
