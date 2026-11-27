#if UNSQUARE_AUTOMATION
using System;
using System.Globalization;
using System.Reflection;
using Newtonsoft.Json.Linq;
using Unsquare.Core.Automation;
using Unsquare.UI.Input;

namespace Unsquare.Automation.Commands
{
    /// <summary>
    /// Reads and changes the options page's values (not key bindings). Changes are applied like the options page
    /// does, but not saved; tests put their values back afterwards.
    /// </summary>
    internal sealed class SettingsCommands
    {
        private readonly UnsquareSettings _settings;

        public SettingsCommands(UnsquareSettings settings) => _settings = settings;

        public void Register(RouteTable<BridgeHandler> routes)
        {
            routes.Add("GET settings", _ => CommandRun.Done(Values()));
            routes.Add("POST settings", Set);
            routes.Add("POST settings/defaults", _ =>
            {
                _settings.SetDefaults();
                _settings.Apply();
                return CommandRun.Done(Values());
            });
        }

        /// <summary>Body: values {"Name": value, …}; enums by name.</summary>
        private CommandRun Set(BridgeArgs args)
        {
            var values = args.Object("values");
            foreach (var property in Properties())
            {
                if (!values.Has(property.Name))
                    continue;
                var type = property.PropertyType;
                object value = type == typeof(bool) ? values.Bool(property.Name)
                    : type == typeof(int) ? values.Int(property.Name)
                    : type == typeof(float) ? values.Float(property.Name)
                    : type.IsEnum ? Enum.Parse(type, values.String(property.Name), true)
                    : values.String(property.Name);
                property.SetValue(_settings, value);
            }
            _settings.Apply();
            return CommandRun.Done(Values());
        }

        private JObject Values()
        {
            var json = new JObject();
            foreach (var property in Properties())
            {
                var value = property.GetValue(_settings);
                json[property.Name] = value switch
                {
                    bool b => b,
                    int i => i,
                    float f => f,
                    Enum e => e.ToString(),
                    _ => Convert.ToString(value, CultureInfo.InvariantCulture),
                };
            }
            return json;
        }

        private static PropertyInfo[] Properties() => Array.FindAll(
            typeof(UnsquareSettings).GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly),
            p => p.CanRead && p.CanWrite && (p.PropertyType.IsPrimitive || p.PropertyType.IsEnum || p.PropertyType == typeof(string)));
    }
}
#endif
