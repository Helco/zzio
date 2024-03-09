using System;
using System.Collections.Generic;

namespace zzre;

public interface IConfigurationSource
{
    string Name { get; }
    bool KeysHaveChanged { get; }
    bool ValuesHaveChanged { get; }

    IEnumerable<string> Keys { get; }
    ConfigurationValue this[string key] { get; }
}

public interface IConfigurationSection
{
    IEnumerable<string> Keys { get; }

    ConfigurationValue this[string key] { get; set; }
}

public interface IConfigurationBinding : IDisposable
{
    void NotifyChanges();
}

public sealed class Configuration
{
    private readonly InMemoryConfigurationSource overwriteSource = new("Overwritten");
    private readonly List<IConfigurationSource> sources;
    private readonly Dictionary<IConfigurationSection, BoundSection> bindings = new();
    private readonly Dictionary<string, IConfigurationSource> keyMapping = new();
    private bool keysHaveChanged;

    public Configuration() => sources = [overwriteSource];

    public void AddSource(IConfigurationSource source)
    {
        sources.Insert(sources.Count - 1, source);
        keysHaveChanged = true;
    }

    public void SetValue(string key, string value) => overwriteSource[key] = new(value);
    public void SetValue(string key, double value) => overwriteSource[key] = new(value);
    public void ResetValue(string key) => overwriteSource.Remove(key);

    public void ApplyChanges()
    {
        var valuesHaveChanged = false;
        foreach (var source in sources)
        {
            keysHaveChanged |= source.KeysHaveChanged;
            valuesHaveChanged |= source.ValuesHaveChanged;
            if (keysHaveChanged)
                break;
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

    private void SetValuesFor(IConfigurationSection instance)
    {
        if (instance.Keys is IReadOnlyList<string> keyList)
        {
            for (int i = 0; i < keyList.Count; i++)
            {
                if (TryGetValue(keyList[i], out var value))
                    instance[keyList[i]] = value;
            }
        }
        else
        {
            foreach (var key in instance.Keys)
            {
                if (TryGetValue(key, out var value))
                    instance[key] = value;
            }
        }
    }

    private void SetValuesFrom(IConfigurationSection instance)
    {
        if (instance.Keys is IReadOnlyList<string> keyList)
        {
            for (int i = 0; i < keyList.Count; i++)
                overwriteSource[keyList[i]] = instance[keyList[i]];
        }
        else
        {
            foreach (var key in instance.Keys)
                overwriteSource[key] = instance[key];
        }
    }

    private bool TryGetValue(string key, out ConfigurationValue value)
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

    public IDisposable Bind<TSection>(TSection instance, Action? onChange = null)
        where TSection : class, IConfigurationSection
    {
        if (!bindings.TryGetValue(instance, out var boundSection))
        {
            bindings.Add(instance, boundSection = new(this, instance));
            SetValuesFor(instance);
        }
        if (onChange is not null)
            boundSection.OnChange += onChange;
        boundSection.AddRef();
        return new Binding(this, instance, onChange);
    }

    private struct Binding(Configuration configuration, IConfigurationSection instance, Action? onChange) : IConfigurationBinding
    {
        private bool wasDisposed;

        public void NotifyChanges() => configuration.SetValuesFrom(instance);

        public void Dispose()
        {
            if (wasDisposed || !configuration.bindings.TryGetValue(instance, out var boundSection))
                return;
            wasDisposed = true;
            if (onChange is not null)
                boundSection.OnChange -= onChange;
            boundSection.DelRef();
        }
    }

    private sealed class BoundSection(Configuration configuration, IConfigurationSection instance)
    {
        private int refCount;
        public event Action? OnChange;

        public void InvokeOnChange() => OnChange?.Invoke();
        public void AddRef() => refCount++;
        public void DelRef()
        {
            refCount--;
            if (refCount <= 0)
                configuration.bindings.Remove(instance);
        }
    }
}
