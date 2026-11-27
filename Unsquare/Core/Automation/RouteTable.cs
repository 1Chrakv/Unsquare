using System;
using System.Collections.Generic;

namespace Unsquare.Core.Automation
{
    /// <summary>
    /// Maps "METHOD path" requests of the automation bridge to handlers. Patterns are like
    /// "GET buildings/{ref}/cage": literal segments match exactly, "{name}" captures one segment.
    /// </summary>
    public sealed class RouteTable<THandler>
    {
        public const string Prefix = "/v1/";

        private readonly List<(string Method, string[] Segments, THandler Handler)> _routes = new();

        public IEnumerable<string> Patterns
        {
            get
            {
                foreach (var route in _routes)
                    yield return route.Method + " " + string.Join("/", route.Segments);
            }
        }

        public void Add(string pattern, THandler handler)
        {
            var space = pattern?.IndexOf(' ') ?? -1;
            if (space <= 0)
                throw new ArgumentException($"Route pattern needs \"METHOD path\": {pattern}.", nameof(pattern));
            var method = pattern.Substring(0, space).ToUpperInvariant();
            var segments = pattern.Substring(space + 1).Trim('/').Split('/');
            foreach (var route in _routes)
                if (route.Method == method && SameShape(route.Segments, segments))
                    throw new ArgumentException($"Duplicate route: {pattern}.", nameof(pattern));
            _routes.Add((method, segments, handler));
        }

        /// <summary>
        /// Finds the handler for a request path like "/v1/buildings/Foo@1,2/cage"; <paramref name="methodAllowed"/>
        /// tells a wrong method (405) apart from an unknown path (404).
        /// </summary>
        public bool TryMatch(string method, string path, out THandler handler,
            out IReadOnlyDictionary<string, string> parameters, out bool methodAllowed)
        {
            handler = default;
            parameters = null;
            methodAllowed = false;
            if (path == null || !path.StartsWith(Prefix, StringComparison.Ordinal))
                return false;
            var segments = path.Substring(Prefix.Length).Trim('/').Split('/');
            foreach (var route in _routes)
            {
                if (!TryBind(route.Segments, segments, out var bound))
                    continue;
                if (!string.Equals(route.Method, method, StringComparison.OrdinalIgnoreCase))
                {
                    methodAllowed = true; // the path exists, just not for this method
                    continue;
                }
                handler = route.Handler;
                parameters = bound;
                methodAllowed = true;
                return true;
            }
            return false;
        }

        private static bool TryBind(string[] pattern, string[] segments, out Dictionary<string, string> bound)
        {
            bound = null;
            if (pattern.Length != segments.Length)
                return false;
            var values = new Dictionary<string, string>();
            for (var i = 0; i < pattern.Length; i++)
            {
                if (IsParameter(pattern[i]))
                {
                    if (segments[i].Length == 0)
                        return false;
                    values[pattern[i].Substring(1, pattern[i].Length - 2)] = Uri.UnescapeDataString(segments[i]);
                }
                else if (!string.Equals(pattern[i], segments[i], StringComparison.Ordinal))
                    return false;
            }
            bound = values;
            return true;
        }

        private static bool SameShape(string[] a, string[] b)
        {
            if (a.Length != b.Length)
                return false;
            for (var i = 0; i < a.Length; i++)
                if (!(IsParameter(a[i]) && IsParameter(b[i])) && a[i] != b[i])
                    return false;
            return true;
        }

        private static bool IsParameter(string segment) =>
            segment.Length > 2 && segment[0] == '{' && segment[segment.Length - 1] == '}';
    }
}
