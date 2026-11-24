using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Unity.Mathematics;

namespace Unsquare.Core.Automation
{
    /// <summary>
    /// Typed access to a bridge request: path parameters, query values and the JSON body. Missing or malformed
    /// values throw <see cref="BridgeArgumentException"/>, which the bridge answers with 400.
    /// </summary>
    public sealed class BridgeArgs
    {
        private readonly JObject _body;
        private readonly IReadOnlyDictionary<string, string> _path;
        private readonly IReadOnlyDictionary<string, string> _query;

        public BridgeArgs(JObject body, IReadOnlyDictionary<string, string> path = null,
            IReadOnlyDictionary<string, string> query = null)
        {
            _body = body ?? new JObject();
            _path = path ?? new Dictionary<string, string>();
            _query = query ?? new Dictionary<string, string>();
        }

        public static BridgeArgs Empty { get; } = new(null);

        public bool Has(string name) => Raw(name) != null;

        public string String(string name) =>
            Raw(name)?.ToString() ?? throw new BridgeArgumentException($"Missing '{name}'.");

        public string String(string name, string fallback) => Raw(name)?.ToString() ?? fallback;

        public float Float(string name) => Convert(name, token => token.Value<float>());
        public float Float(string name, float fallback) => Has(name) ? Float(name) : fallback;
        public int Int(string name) => Convert(name, token => token.Value<int>());
        public int Int(string name, int fallback) => Has(name) ? Int(name) : fallback;
        public bool Bool(string name) => Convert(name, token => token.Value<bool>());
        public bool Bool(string name, bool fallback) => Has(name) ? Bool(name) : fallback;

        /// <summary>A vector as [x, y, z] or {"x":…, "y":…, "z":…}; missing object members are 0.</summary>
        public float3 Float3(string name)
        {
            var token = Raw(name) ?? throw new BridgeArgumentException($"Missing '{name}'.");
            try
            {
                if (token is JArray array && array.Count == 3)
                    return new float3(array[0].Value<float>(), array[1].Value<float>(), array[2].Value<float>());
                if (token is JObject obj)
                    return new float3(obj.Value<float?>("x") ?? 0f, obj.Value<float?>("y") ?? 0f,
                        obj.Value<float?>("z") ?? 0f);
            }
            catch (Exception e) when (e is FormatException or InvalidCastException)
            {
                // reported below
            }
            throw new BridgeArgumentException($"'{name}' must be [x, y, z] or {{x, y, z}}.");
        }

        public T Enum<T>(string name) where T : struct
        {
            var text = String(name);
            if (System.Enum.TryParse(text, true, out T value) && System.Enum.IsDefined(typeof(T), value))
                return value;
            throw new BridgeArgumentException(
                $"'{name}' must be one of {string.Join(", ", System.Enum.GetNames(typeof(T)))}, not '{text}'.");
        }

        public T Enum<T>(string name, T fallback) where T : struct => Has(name) ? Enum<T>(name) : fallback;

        /// <summary>A nested JSON object, e.g. {"rotate": {"axis": 1, "degrees": 30}}.</summary>
        public BridgeArgs Object(string name) =>
            Raw(name) is JObject nested
                ? new BridgeArgs(nested)
                : throw new BridgeArgumentException($"'{name}' must be a JSON object.");

        private T Convert<T>(string name, Func<JToken, T> convert)
        {
            var token = Raw(name) ?? throw new BridgeArgumentException($"Missing '{name}'.");
            try
            {
                return convert(token);
            }
            catch (Exception e) when (e is FormatException or InvalidCastException or OverflowException)
            {
                throw new BridgeArgumentException($"'{name}' has the wrong type: {token}.");
            }
        }

        // Path parameters win over the body, the body over the query string.
        private JToken Raw(string name)
        {
            if (_path.TryGetValue(name, out var fromPath))
                return new JValue(fromPath);
            var fromBody = _body[name];
            if (fromBody != null && fromBody.Type != JTokenType.Null)
                return fromBody;
            return _query.TryGetValue(name, out var fromQuery) && fromQuery != null ? new JValue(fromQuery) : null;
        }
    }
}
