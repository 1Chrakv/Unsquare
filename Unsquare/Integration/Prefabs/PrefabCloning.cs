using System;
using System.Reflection;
using Game.Prefabs;

namespace Unsquare.Integration.Prefabs
{
    /// <summary>
    /// Guards against <see cref="PrefabBase.Clone"/> sharing mutable state with its source:
    /// anything we modify on a clone is detached first, so the base prefab never changes.
    /// </summary>
    internal static class PrefabCloning
    {
        private static readonly MethodInfo MemberwiseCloneMethod =
            typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic);

        /// <summary>The clone's own instance of <typeparamref name="T"/>, or null if it has none.</summary>
        public static T GetOwnedComponent<T>(PrefabBase clone, PrefabBase source) where T : ComponentBase
        {
            if (!clone.TryGetExactly(out T component))
                return null;
            if (!source.TryGetExactly(out T original) || !ReferenceEquals(component, original))
                return component;

            clone.Remove<T>();
            component = clone.AddComponentFrom(original);
            if (ReferenceEquals(component, original))
                throw new InvalidOperationException($"Cannot detach {typeof(T).Name} from {source.name}.");
            return component;
        }

        /// <summary>Shallow copy that keeps any fields added by future game versions.</summary>
        public static ObjectMeshInfo Copy(ObjectMeshInfo info) => (ObjectMeshInfo)MemberwiseCloneMethod.Invoke(info, null);
    }
}
