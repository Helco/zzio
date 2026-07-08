using System;
using System.Collections.Generic;
using System.Linq;

namespace zzre;

/// <summary>
/// A configuration source is a named set of configuration values.
/// It is used as one layer of ground truth for configuration values (i.e. not overwritten by some runtime process).
/// </summary>
/// <remarks>
/// These values or the set of values (<see cref="Keys"/>) can change with the source
/// passively notifying this behavior (<see cref="KeysHaveChanged"/> and <see cref="ValuesHaveChanged"/>)
/// </remarks>
public interface IConfigurationSource
{
    string Name { get; }
    bool KeysHaveChanged { get; }
    bool ValuesHaveChanged { get; }

    /// <summary>Should reset <see cref="KeysHaveChanged"/> and <see cref="ValuesHaveChanged"/> to <c>false</c></summary>
    void ResetChanged();

    IEnumerable<string> Keys { get; }
    ConfigurationValue this[string key] { get; }
}

/// <summary>
/// A configuration section is the consumption point of a set of configuration values.
/// They are bound to the <see cref="Configuration"/> object which will update the values upon changes.
/// A section can also be used to overwrite values temporarily, however changes will only be propagated
/// when <see cref="IConfigurationBinding.NotifyChanges"/> is called.
/// </summary>
/// <remarks>
/// Usually this interface is implemented by the source generation when a class has <see cref="ConfigurationAttribute"/> annotations.
/// </remarks>
public interface IConfigurationSection
{
    /// <summary>The requested set of keys for which the configuration values are set to this instance</summary>
    IEnumerable<string> Keys { get; }

    /// <summary>The property for values to be set by or to retrieve changed values</summary>
    /// <param name="key">The key of the configuration value to set/get</param>
    /// <returns>The configuration value</returns>
    ConfigurationValue this[string key] { get; set; }
}

/// <summary>
/// An interface controlling the binding of a <see cref="IConfigurationSection"/> instance at the central <see cref="Configuration"/> object
/// </summary>
/// <remarks>Disposing the binding will sever the connection between the section instance and the <see cref="Configuration"/> object</remarks>
public interface IConfigurationBinding : IDisposable
{
    /// <summary>Calling this function will propagate all changes from the <see cref="IConfigurationSection"/> as overwritten values</summary>
    void NotifyChanges();
}

/// <summary>
/// A central configuration object containing a hierachy of <see cref="IConfigurationSource"/> to retrieve values and 
/// <see cref="IConfigurationSection"/> to propagate configuration values.
/// </summary>
public sealed partial class Configuration
{
    private readonly InMemoryConfigurationSource overwriteSource = new("Application");
    private readonly List<IConfigurationSource> sources;
    private readonly Dictionary<IConfigurationSection, BoundSection> bindings = [];
    private readonly Dictionary<string, IConfigurationSource> keyMapping = [];
    private bool keysHaveChanged;

    /// <summary>The set of all registered configuration keys</summary>
    public IReadOnlyCollection<string> Keys => keyMapping.Keys;
    /// <summary>A counter which will change whenever the set of keys change</summary>
    /// <remarks>A change of values will not increment this counter</remarks>
    public int KeyVersion { get; private set; }

    public Configuration() : this(useDefaultSource: true) { }
    internal Configuration(bool useDefaultSource)
    {
        sources = useDefaultSource
            ? [defaultSource, overwriteSource]
            : [overwriteSource];
        ApplyChanges(); // to set up key mappings from default 
    }

    /// <summary>Adds a new <see cref="IConfigurationSource"/> to the <see cref="Configuration"/>.</summary>
    /// <remarks>It will added as the highest-priority source (except for overwritten values)</remarks>
    /// <param name="source"></param>
    public void AddSource(IConfigurationSource source)
    {
        sources.Insert(sources.Count - 1, source);
        keysHaveChanged = true;
    }

    /// <summary>Overwrites a single configuration value</summary>
    /// <param name="key">The key of the configuration value</param>
    /// <param name="value">The value of the configuration value</param>
    public void SetValue(string key, string value) => overwriteSource[key] = new(value);

    /// <summary>Overwrites a single configuration value</summary>
    /// <param name="key">The key of the configuration value</param>
    /// <param name="value">The value of the configuration value</param>
    public void SetValue(string key, double value) => overwriteSource[key] = new(value);

    /// <summary>Removes an overwritten value, so that its value will revert to the appropriate value from an <see cref="IConfigurationSource"/></summary>
    /// <param name="key">The key of the configuration value</param>
    public void ResetValue(string key) => overwriteSource.Remove(key);

    /// <summary>Determines whether a configuration value was temporarily overwritten</summary>
    /// <param name="key">The key of the configuration value</param>
    /// <returns>Whether the value was overwritten</returns>
    public bool IsOverwritten(string key) => overwriteSource.Keys.Contains(key);

    /// <summary>Applies all overwritten values or added sources to all sections.</summary>
    /// <remarks>This method should be called frequently, as otherwise changes in configuration will not reach the <see cref="IConfigurationSection"/> instances</remarks>
    public void ApplyChanges()
    {
        var valuesHaveChanged = false;
        foreach (var source in sources)
        {
            keysHaveChanged |= source.KeysHaveChanged;
            valuesHaveChanged |= source.ValuesHaveChanged;
            source.ResetChanged();
        }
        valuesHaveChanged |= keysHaveChanged;

        if (keysHaveChanged)
            UpdateKeyMapping();

        if (valuesHaveChanged)
        {
            foreach (var section in bindings.Keys)
                SetValuesFor(section);
        }
    }

    private void UpdateKeyMapping()
    {
        KeyVersion++;
        keyMapping.Clear();
        for (int i = sources.Count - 1; i >= 0; i--)
        {
            if (sources[i].Keys is IReadOnlyList<string> keyList)
            {
                for (int j = 0; j < keyList.Count; j++)
                    keyMapping.TryAdd(keyList[j], sources[i]);
            }
            else
            {
                foreach (var key in sources[i].Keys)
                    keyMapping.TryAdd(key, sources[i]);
            }
        }
        keysHaveChanged = false;
    }

    private void SetValuesFor(IConfigurationSection instance, bool setAsDefault = false)
    {
        if (instance.Keys is IReadOnlyList<string> keyList)
        {
            for (int i = 0; i < keyList.Count; i++)
            {
                if (TryGetValue(keyList[i], out var value))
                    instance[keyList[i]] = value;
                else if (setAsDefault)
                    defaultSource[keyList[i]] = instance[keyList[i]];
            }
        }
        else
        {
            foreach (var key in instance.Keys)
            {
                if (TryGetValue(key, out var value))
                    instance[key] = value;
                else if (setAsDefault)
                    defaultSource[key] = instance[key];
            }
        }
    }

    private void SetValuesFrom(IConfigurationSection instance)
    {
        if (instance.Keys is IReadOnlyList<string> keyList)
        {
            for (int i = 0; i < keyList.Count; i++)
                OverwriteValue(keyList[i], instance[keyList[i]]);
        }
        else
        {
            foreach (var key in instance.Keys)
                OverwriteValue(key, instance[key]);
        }
    }

    private void OverwriteValue(string key, ConfigurationValue value)
    {
        if (!IsOverwritten(key) && TryGetValue(key, out var prevValue) && prevValue == value)
            return;
        overwriteSource[key] = value;
    }

    /// <summary>Retrieves a single configuration value by key</summary>
    /// <param name="key">The key of the configuration value</param>
    /// <param name="value">The configuration value</param>
    /// <returns>Whether the configuration value exists</returns>
    public bool TryGetValue(string key, out ConfigurationValue value)
    {
        if (keyMapping.TryGetValue(key, out var source))
        {
            value = source[key];
            return true;
        }
        else
        {
            value = default;
            return false;
        }
    }

    /// <summary>Retrieves a single configuration value by key</summary>
    /// <param name="key">The key of the configuration value</param>
    /// <returns>The configuration value</returns>
    public ConfigurationValue GetValue(string key) =>
        TryGetValue(key, out var value) ? value
        : throw new KeyNotFoundException($"Configuration key not found: {key}");

    /// <summary>
    /// Binds a new <see cref="IConfigurationSection"/> instance to the <see cref="Configuration"/>, thus enabling changes to be propagated to it.
    /// </summary>
    /// <typeparam name="TSection">The specific type of the section instance</typeparam>
    /// <param name="instance">The instance to bind to</param>
    /// <returns>The <see cref="IConfigurationBinding"/> instance with which to unbind the section or to notify overwritten values by the section</returns>
    public IConfigurationBinding Bind<TSection>(TSection instance)
        where TSection : class, IConfigurationSection
    {
        if (!bindings.TryGetValue(instance, out var boundSection))
        {
            bindings.Add(instance, boundSection = new(this, instance));
            SetValuesFor(instance, setAsDefault: true);
        }
        boundSection.AddRef();
        return new Binding(this, instance);
    }

    private struct Binding(Configuration configuration, IConfigurationSection instance) : IConfigurationBinding
    {
        private bool wasDisposed;

        public void NotifyChanges() => configuration.SetValuesFrom(instance);

        public void Dispose()
        {
            if (wasDisposed || !configuration.bindings.TryGetValue(instance, out var boundSection))
                return;
            wasDisposed = true;
            boundSection.DelRef();
        }
    }

    private sealed class BoundSection(Configuration configuration, IConfigurationSection instance)
    {
        private int refCount;

        public void AddRef() => refCount++;
        public void DelRef()
        {
            refCount--;
            if (refCount <= 0)
                configuration.bindings.Remove(instance);
        }
    }
}
