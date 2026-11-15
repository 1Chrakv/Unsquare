using System;
using System.Reflection;
using Unity.Entities;
using Unity.Mathematics;
using Unsquare.Core.PlacemakerCompatibility;
using Unsquare.Core.Interop;

namespace Unsquare.Integration.PlacemakerCompatibility
{
    /// <summary>
    /// Reads Placemaker's bent row houses through the public members of its shape system, found by reflection (no
    /// compile-time reference). The whole contract is checked at load; a changed member turns off only its feature.
    /// </summary>
    internal sealed class PlacemakerBridge
    {
        /// <summary>The Placemaker version this bridge was last checked against in game.</summary>
        public const string TestedWith = "1.4.2";

        private const string AssemblyName = "Placemaker";
        private const string SystemTypeName = "Placemaker.Game.Shapes.ShapeSystem";
        private static readonly string[] ShapeFieldNames =
            { "Min", "Max", "BackLeft", "BackRight", "FrontLeft", "FrontRight" };

        private readonly object _system;
        private readonly Func<Entity, bool> _isVariant;
        private readonly MethodInfo _baseOf;
        private readonly MethodInfo _isRequested;
        private readonly MethodInfo _tryGetCage;
        private readonly FieldInfo[] _shapeFields;
        private readonly MethodInfo _tryGetPlanned;
        private readonly FieldInfo[] _plannedFields; // JoinedLeft, JoinedRight, LeftNeighbour, RightNeighbour
        private bool _detectBroken;
        private bool _convertBroken;
        private bool _rowsBroken;

        private PlacemakerBridge(object system, string version, Func<Entity, bool> isVariant, MethodInfo baseOf,
            MethodInfo isRequested, MethodInfo tryGetCage, FieldInfo[] shapeFields, MethodInfo tryGetPlanned,
            FieldInfo[] plannedFields)
        {
            _system = system;
            Version = version;
            _isVariant = isVariant;
            _baseOf = baseOf;
            _isRequested = isRequested;
            _tryGetCage = tryGetCage;
            _shapeFields = shapeFields;
            _tryGetPlanned = tryGetPlanned;
            _plannedFields = plannedFields;
        }

        public string Version { get; }

        /// <summary>Tells Placemaker's variant prefabs, including light copies that carry the plain prefab's name.</summary>
        public bool CanDetect => _isVariant != null && !_detectBroken;

        /// <summary>Reads a variant's base and shape and whether Placemaker is still reshaping a building.</summary>
        public bool CanConvert => _baseOf != null && !_convertBroken;

        /// <summary>Reads which neighbours Placemaker joined a house to.</summary>
        public bool CanReadRows => _tryGetPlanned != null && !_rowsBroken;

        /// <summary>
        /// False if Placemaker is not loaded or has no shape system; otherwise a bridge with the features its
        /// members allow, after logging one status line.
        /// </summary>
        public static bool TryFind(World world, out PlacemakerBridge bridge)
        {
            bridge = null;
            try
            {
                Type type = null;
                Assembly found = null;
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (assembly.GetName().Name != AssemblyName)
                        continue;
                    found = assembly;
                    type = assembly.GetType(SystemTypeName, false);
                    break;
                }
                if (found == null)
                    return false;
                string version = VersionOf(found);
                if (type == null)
                {
                    Mod.Log.Warn($"Placemaker {version} (tested with {TestedWith}) has no {SystemTypeName}; " +
                                 "Unsquare treats its buildings as plain ones.");
                    return false;
                }
                var system = type.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public)?.GetValue(null)
                             ?? world.GetExistingSystemManaged(type);
                if (system == null)
                {
                    Mod.Log.Warn($"Placemaker {version}: its shape system is not running.");
                    return false;
                }

                var detect = new ReflectionContract(type);
                var isVariant = detect.Method("IsVariant", typeof(bool), typeof(Entity));

                var convert = new ReflectionContract(type);
                var baseOf = convert.Method("BaseOf", typeof(Entity), typeof(Entity));
                var isRequested = convert.Method("IsRequested", typeof(bool), typeof(Entity));
                var tryGetCage = convert.TryGetter("TryGetCage", typeof(Entity), out var shapeType);
                var shapeFields = Fields(convert, shapeType, ShapeFieldNames, typeof(float2), typeof(float2),
                    typeof(float2), typeof(float2), typeof(float2), typeof(float2));

                var rows = new ReflectionContract(type);
                var tryGetPlanned = rows.TryGetter("TryGetPlanned", typeof(Entity), out var plannedType);
                var plannedFields = Fields(rows, plannedType,
                    new[] { "JoinedLeft", "JoinedRight", "LeftNeighbour", "RightNeighbour" },
                    typeof(bool), typeof(bool), typeof(Entity), typeof(Entity));

                string status = $"Placemaker {version} (tested with {TestedWith}): detect {detect.Describe()}, " +
                                $"convert {convert.Describe()}, rows {rows.Describe()}.";
                if (detect.IsComplete && convert.IsComplete && rows.IsComplete)
                    Mod.Log.Info(status);
                else
                    Mod.Log.Warn(status + " The missing features are off; please report this with Unsquare.Mod.log.");

                // Asked every frame for the hovered and edited building, so no reflection call per question.
                var ask = detect.IsComplete
                    ? (Func<Entity, bool>)Delegate.CreateDelegate(typeof(Func<Entity, bool>), system, isVariant)
                    : null;
                bridge = convert.IsComplete
                    ? new PlacemakerBridge(system, version, ask, baseOf, isRequested, tryGetCage, shapeFields,
                        rows.IsComplete ? tryGetPlanned : null, plannedFields)
                    : new PlacemakerBridge(system, version, ask, null, null, null, null,
                        rows.IsComplete ? tryGetPlanned : null, plannedFields);
                return true;
            }
            catch (Exception e)
            {
                Mod.Log.Error(e, "Looking up Placemaker's shape system failed.");
                return false;
            }
        }

        private static FieldInfo[] Fields(ReflectionContract owner, Type type, string[] names, params Type[] types)
        {
            if (type == null)
                return null;
            var contract = new ReflectionContract(type);
            var fields = new FieldInfo[names.Length];
            for (int i = 0; i < names.Length; i++)
                fields[i] = contract.Field(names[i], types[i]);
            owner.Include(contract);
            return contract.IsComplete ? fields : null;
        }

        private static string VersionOf(Assembly assembly)
        {
            var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion;
            if (string.IsNullOrEmpty(informational))
                return assembly.GetName().Version?.ToString(3) ?? "?";
            int build = informational.IndexOf('+');
            return build > 0 ? informational.Substring(0, build) : informational;
        }

        /// <summary>
        /// True for a prefab entity Placemaker bent. Its light copies carry the plain prefab's identity, so the
        /// prefab's name does not tell.
        /// </summary>
        public bool IsVariant(Entity prefab)
        {
            if (!CanDetect)
                return false;
            try
            {
                return _isVariant(prefab);
            }
            catch (Exception e)
            {
                TurnOff(ref _detectBroken, "telling its buildings", e);
                return false;
            }
        }

        /// <summary>The plain prefab a Placemaker variant prefab was bent from.</summary>
        public bool TryGetBase(Entity variantPrefab, out Entity basePrefab)
        {
            basePrefab = Entity.Null;
            if (!CanConvert)
                return false;
            try
            {
                if (_baseOf.Invoke(_system, new object[] { variantPrefab }) is Entity found && found != variantPrefab)
                    basePrefab = found;
            }
            catch (Exception e)
            {
                TurnOff(ref _convertBroken, "reading a building's base", e);
            }
            return basePrefab != Entity.Null;
        }

        /// <summary>The shape a Placemaker variant prefab was bent with, in building space.</summary>
        public bool TryGetCage(Entity variantPrefab, out PlacemakerCage cage)
        {
            cage = default;
            if (!CanConvert)
                return false;
            try
            {
                var args = new object[] { variantPrefab, null };
                if (!(bool)_tryGetCage.Invoke(_system, args) || args[1] == null)
                    return false;
                var values = new float2[_shapeFields.Length];
                for (int i = 0; i < values.Length; i++)
                    values[i] = (float2)_shapeFields[i].GetValue(args[1]);
                if (!math.all(math.isfinite(values[0])) || !math.all(math.isfinite(values[1])))
                {
                    Mod.Log.Warn($"Placemaker gave prefab {variantPrefab} a span that is not finite.");
                    return false;
                }
                cage = new PlacemakerCage(values[0], values[1], values[2], values[3], values[4], values[5]);
                return true;
            }
            catch (Exception e)
            {
                TurnOff(ref _convertBroken, "reading a building's shape", e);
                return false;
            }
        }

        /// <summary>
        /// Whether Placemaker is about to give the building another shape (asked, baking or swapping); false if that
        /// can't be read, which is not the same as "not busy".
        /// </summary>
        public bool TryIsBusyWith(Entity building, out bool busy)
        {
            busy = false;
            if (!CanConvert)
                return false;
            try
            {
                busy = (bool)_isRequested.Invoke(_system, new object[] { building });
                return true;
            }
            catch (Exception e)
            {
                TurnOff(ref _convertBroken, "asking whether it reshapes a building", e);
                return false;
            }
        }

        /// <summary>
        /// The neighbours Placemaker joined the building to in its row plan (Entity.Null where there is no joint);
        /// false if the plan can't be read or doesn't hold the building.
        /// </summary>
        public bool TryGetJoinedNeighbours(Entity building, out Entity left, out Entity right)
        {
            left = Entity.Null;
            right = Entity.Null;
            if (!CanReadRows)
                return false;
            try
            {
                var args = new object[] { building, null };
                if (!(bool)_tryGetPlanned.Invoke(_system, args) || args[1] == null)
                    return false;
                var planned = args[1];
                if ((bool)_plannedFields[0].GetValue(planned))
                    left = (Entity)_plannedFields[2].GetValue(planned);
                if ((bool)_plannedFields[1].GetValue(planned))
                    right = (Entity)_plannedFields[3].GetValue(planned);
                return true;
            }
            catch (Exception e)
            {
                TurnOff(ref _rowsBroken, "reading its row plan", e);
                return false;
            }
        }

        // Members can pass the load check and still fail at runtime (changed meaning, internal errors).
        private void TurnOff(ref bool broken, string what, Exception e)
        {
            if (broken)
                return;
            broken = true;
            Mod.Log.Error(e, $"Placemaker {Version} (tested with {TestedWith}) failed {what}; that feature is off " +
                             "until the save is loaded again.");
        }
    }
}
