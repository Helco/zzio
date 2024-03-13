using System.Collections.Generic;
using System.Linq;
using zzio;

namespace zzre;

public sealed class VarConfigurationSource(string name, VarConfig config, string section = "") : IConfigurationSource
{
    public string Name => name;
    public bool KeysHaveChanged => false;
    public bool ValuesHaveChanged => false;
    public IEnumerable<string> Keys { get; } =
        config.variables.Keys.Select(k => $"{section}.{k}").ToArray();

    public ConfigurationValue this[string key]
    {
        get
        {
            if (!key.StartsWith(key, System.StringComparison.Ordinal))
                throw new KeyNotFoundException();
            var v = config.variables[key.Substring(section.Length + 1)];
            return string.IsNullOrEmpty(v.stringValue)
                ? new(v.floatValue)
                : new(v.stringValue);
        }
    }
}
