using System;
using System.Collections.Generic;
using System.Reflection;

namespace Unsquare.Core.Interop
{
    /// <summary>
    /// Looks up the public members another mod is expected to have and records what is missing instead of
    /// throwing, so a changed mod can be reported in one line and only the features using those members turn off.
    /// </summary>
    public sealed class ReflectionContract
    {
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public;
        private readonly List<string> _missing = new();

        public ReflectionContract(Type type)
        {
            Type = type ?? throw new ArgumentNullException(nameof(type));
        }

        public Type Type { get; }
        public IReadOnlyList<string> Missing => _missing;
        public bool IsComplete => _missing.Count == 0;

        /// <summary>A public instance method with exactly these parameter types and this return type; null if missing.</summary>
        public MethodInfo Method(string name, Type returnType, params Type[] parameterTypes)
        {
            // Exact types only: the default binder would accept widened ones (int for long).
            foreach (var method in Type.GetMethods(Instance))
            {
                if (method.Name == name && method.ReturnType == returnType &&
                    HasParameters(method, parameterTypes))
                    return method;
            }
            _missing.Add($"{Type.Name}.{name}");
            return null;
        }

        private static bool HasParameters(MethodInfo method, Type[] types)
        {
            var parameters = method.GetParameters();
            if (parameters.Length != types.Length)
                return false;
            for (int i = 0; i < types.Length; i++)
            {
                if (parameters[i].ParameterType != types[i])
                    return false;
            }
            return true;
        }

        /// <summary>
        /// A public instance method <c>bool name(key, out value)</c>; <paramref name="valueType"/> is the out
        /// parameter's type (its fields can then be checked with a contract of their own). Null if missing.
        /// </summary>
        public MethodInfo TryGetter(string name, Type keyType, out Type valueType)
        {
            valueType = null;
            foreach (var method in Type.GetMethods(Instance))
            {
                if (method.Name != name || method.ReturnType != typeof(bool))
                    continue;
                var parameters = method.GetParameters();
                if (parameters.Length != 2 || parameters[0].ParameterType != keyType || !parameters[1].IsOut)
                    continue;
                valueType = parameters[1].ParameterType.GetElementType();
                return method;
            }
            _missing.Add($"{Type.Name}.{name}");
            return null;
        }

        /// <summary>A public instance field of exactly this type; null if missing.</summary>
        public FieldInfo Field(string name, Type fieldType)
        {
            var field = Type.GetField(name, Instance);
            if (field == null || field.FieldType != fieldType)
            {
                _missing.Add($"{Type.Name}.{name}");
                return null;
            }
            return field;
        }

        /// <summary>Takes over what another contract (e.g. on an out parameter's type) found missing.</summary>
        public void Include(ReflectionContract other)
        {
            if (other != null)
                _missing.AddRange(other._missing);
        }

        /// <summary>"ok", or the missing members.</summary>
        public string Describe() => IsComplete ? "ok" : "missing " + string.Join(", ", _missing);
    }
}
