using System.Collections.Generic;

namespace zzre;

public sealed class InMemoryConfigurationSource(string name) : IConfigurationSource
{
    private readonly Dictionary<string, ConfigurationValue> variables = [];
    private bool keysHaveChanged;

    public string Name => name;
    public bool KeysHaveChanged
    {
        get
        {
            var prev = keysHaveChanged;
            keysHaveChanged = false;
            return prev;
        }
    }
    public bool ValuesHaveChanged
    {
        get
        {
            var prev = field;
            field = false;
            return prev;
        }

        private set;
    }
    public IEnumerable<string> Keys => variables.Keys;

    public ConfigurationValue this[string key]
    {
        get => variables[key];
        set
        {
            ValuesHaveChanged = true;
            if (variables.TryAdd(key, value))
                keysHaveChanged = true;
            else
                variables[key] = value;
        }
    }

    public void Remove(string key) =>
        keysHaveChanged |= variables.Remove(key);
}