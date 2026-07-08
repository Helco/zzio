using System.Collections.Generic;

namespace zzre;

/// <summary>
/// A <see cref="IConfigurationSource"/> that stores all values in memory
/// </summary>
/// <param name="name">The name of the source</param>
public sealed class InMemoryConfigurationSource(string name) : IConfigurationSource
{
    private readonly Dictionary<string, ConfigurationValue> variables = [];

    public string Name => name;
    public bool KeysHaveChanged { get; private set; }
    public bool ValuesHaveChanged { get; private set; }
    public IEnumerable<string> Keys => variables.Keys;

    public ConfigurationValue this[string key]
    {
        get => variables[key];
        set
        {
            ValuesHaveChanged = true;
            if (variables.TryAdd(key, value))
                KeysHaveChanged = true;
            else
                variables[key] = value;
        }
    }

    public void Remove(string key) =>
        KeysHaveChanged |= variables.Remove(key);

    public void ResetChanged() => KeysHaveChanged = ValuesHaveChanged = false;
}