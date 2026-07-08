using System.Collections.Generic;

namespace zzre;

/// <summary>
/// An adapter to use any <see cref="IConfigurationSection"/> as <see cref="IConfigurationSource"/>
/// </summary>
/// <remarks>Changes in the section will not mark the source as modified!</remarks>
/// <param name="name"></param>
/// <param name="section"></param>
public sealed class ConfigurationSectionAsSource(string name, IConfigurationSection section) : IConfigurationSource
{
    public string Name => name; 
    public bool KeysHaveChanged => false;
    public bool ValuesHaveChanged => false;

    public IEnumerable<string> Keys => section.Keys;
    public ConfigurationValue this[string key] => section[key];
    public void ResetChanged() { }
}
