using Newtonsoft.Json.Linq;
using NUnit.Framework;
using NUnit.Framework.Constraints;
using System;
using System.Collections.Generic;
using static zzio.GameConfig;

namespace zzre.tests;

public sealed class TestInMemoryConfigurationSource
{
    private InMemoryConfigurationSource source = null!;

    [SetUp]
    public void SetUp()
    {
        source = new("TestSource");
    }

    [Test]
    public void Name() => Assert.That(source.Name, Is.EqualTo("TestSource"));

    [Test]
    public void AddingNew()
    {
        source["abc"] = new(3.0);
        Assert.That(source.KeysHaveChanged, Is.True);
        Assert.That(source.ValuesHaveChanged, Is.True);

        source.ResetChanged();
        Assert.That(source.KeysHaveChanged, Is.False);
        Assert.That(source.ValuesHaveChanged, Is.False);

        source["def"] = new("abc");
        source["ghi"] = new(3.141592653);
        Assert.That(source.KeysHaveChanged, Is.True);
        Assert.That(source.ValuesHaveChanged, Is.True);
    }

    [Test]
    public void Overwriting()
    {
        source["abc"] = new(3.0);
        Assert.That(source.KeysHaveChanged, Is.True);
        Assert.That(source.ValuesHaveChanged, Is.True);

        source.ResetChanged();
        Assert.That(source.KeysHaveChanged, Is.False);
        Assert.That(source.ValuesHaveChanged, Is.False);

        source["abc"] = new("def");
        Assert.That(source.KeysHaveChanged, Is.False);
        Assert.That(source.ValuesHaveChanged, Is.True);
    }

    [Test]
    public void RemovingKeys()
    {
        source["abc"] = new(3.0);
        Assert.That(source.KeysHaveChanged, Is.True);
        source.ResetChanged();
        Assert.That(source.KeysHaveChanged, Is.False);

        source.Remove("abc");
        Assert.That(source.KeysHaveChanged, Is.True);
        source.ResetChanged();
        Assert.That(source.KeysHaveChanged, Is.False);

        source.Remove("def");
        Assert.That(source.KeysHaveChanged, Is.False);
    }

    [Test]
    public void RetrievingKeys()
    {
        source["abc"] = new(3.0);
        Assert.That(source["abc"].IsNumeric, Is.True);
        Assert.That(source["abc"].IsString, Is.False);
        Assert.That(source["abc"].Numeric, Is.EqualTo(3.0));

        source["abc"] = new("def");
        Assert.That(source["abc"].IsNumeric, Is.False);
        Assert.That(source["abc"].IsString, Is.True);
        Assert.That(source["abc"].String, Is.EqualTo("def"));
    }

    [Test]
    public void RetrievingAllKeys()
    {
        Assert.That(source.Keys, Is.Empty);

        source["abc"] = new(3.0);
        Assert.That(source.Keys, Is.EquivalentTo(["abc"]));

        source["def"] = new("def");
        Assert.That(source.Keys, Is.EquivalentTo(["abc", "def"]));

        source.Remove("abc");
        Assert.That(source.Keys, Is.EquivalentTo(["def"]));

        source.Remove("def");
        Assert.That(source.Keys, Is.Empty);
    }
}

internal sealed class InMemoryConfigurationSection(Dictionary<string, ConfigurationValue> values) : IConfigurationSection
{
    public ConfigurationValue this[string key]
    {
        get => values[key];
        set => values[key] = value;
    }

    public IEnumerable<string> Keys => values.Keys;
}

public sealed class TestConfigurationSectionAsSource
{
    private readonly ConfigurationSectionAsSource source = new("TestSource", new InMemoryConfigurationSection(new()
    {
        { "abc", new(3.0) },
        { "def", new("ghi") }
    }));

    [Test]
    public void BasicAttributes()
    {
        Assert.That(source.Name, Is.EqualTo("TestSource"));
        Assert.That(source.KeysHaveChanged, Is.False);
        Assert.That(source.ValuesHaveChanged, Is.False);
        Assert.That(source.ResetChanged, Throws.Nothing);
    }

    [Test]
    public void Keys()
    {
        Assert.That(source.Keys, Is.EquivalentTo(["abc", "def"]));
    }

    [Test]
    public void Values()
    {
        Assert.That(source["abc"], Is.EqualTo(3.0));
        Assert.That(source["def"], Is.EqualTo("ghi"));
        Assert.That(() => source["alskdjaslkdj"], Throws.InstanceOf<KeyNotFoundException>());
    }
}

public sealed class TestGameConfigSource
{
    [Test]
    public void BasicAttributes()
    {
        var source = GameConfigSource.Default;
        Assert.That(source.Keys, Contains.Item("zanzarah.game.SoundVolume"));
        Assert.That(source.KeysHaveChanged, Is.False);
        Assert.That(source.ValuesHaveChanged, Is.False);
    }
}

public sealed class TestVarConfigurationSource
{
    private VarConfigurationSource source;

    [SetUp]
    public void SetUp()
    {
        var varConfig = new zzio.VarConfig();
        varConfig.variables = new()
        {
            { "abc", new(3.0f) },
            { "def", new("ghi") }
        };
        source = new VarConfigurationSource("TestSource", varConfig, "test");
    }

    [Test]
    public void BasicAttributes()
    {
        Assert.That(source.Name, Is.EqualTo("TestSource"));
        Assert.That(source.KeysHaveChanged, Is.False);
        Assert.That(source.ValuesHaveChanged, Is.False);
        Assert.That(source.ResetChanged, Throws.Nothing);
    }

    [Test]
    public void Keys()
    {
        Assert.That(source.Keys, Is.EquivalentTo(["test.abc", "test.def"]));
    }

    [Test]
    public void Values()
    {
        Assert.That(source["test.abc"], Is.EqualTo(3.0));
        Assert.That(source["test.def"], Is.EqualTo("ghi"));
        Assert.That(() => source["alskdjaslkdj"], Throws.InstanceOf<KeyNotFoundException>());
        Assert.That(() => source["test.alskdjaslkdj"], Throws.InstanceOf<KeyNotFoundException>());
    }
}

public sealed class TestConfiguration
{
    private Configuration config = null!;
    private InMemoryConfigurationSource source1 = null!, source2 = null!;
    private InMemoryConfigurationSection section = null!;

    [SetUp]
    public void SetUp()
    {
        config = new(useDefaultSource: false);

        source1 = new("Source 1");
        source1["excl1"] = new(3.0);
        source1["def"] = new("source1");

        source2 = new("Source 2");
        source2["excl2"] = new(3.1);
        source2["def"] = new("source2");

        section = new(new()
        { // deliberately wrong values (except for exclSection)
            { "excl1", new(double.PositiveInfinity) },
            { "excl2", new(double.NegativeInfinity) },
            { "def", new("garbage") },
            { "exclSection", new("section") }
        });
    }

    [Test]
    public void EmptyConfiguration()
    {
        Assert.That(config.Keys, Is.Empty);
        Assert.That(config.KeyVersion, Is.Zero);
        Assert.That(config.ApplyChanges, Throws.Nothing);
        Assert.That(config.TryGetValue("def", out _), Is.False);
    }

    [Test]
    public void AddSourceAffectsKeys()
    {
        config.AddSource(source1);
        config.ApplyChanges();
        Assert.That(config.Keys, Is.EquivalentTo(["excl1", "def"]));
        Assert.That(config.KeyVersion, Is.EqualTo(1));

        config.AddSource(source2);
        config.ApplyChanges();
        Assert.That(config.Keys, Is.EquivalentTo(["excl1", "excl2", "def"]));
        Assert.That(config.KeyVersion, Is.EqualTo(2));

        config.ApplyChanges();
        Assert.That(config.KeyVersion, Is.EqualTo(2));
    }

    [Test]
    public void AddSourceAddsHighestPriority()
    {
        Assert.That(config.GetControllingSourceName("def"), Is.Null);

        config.AddSource(source1);
        config.ApplyChanges();
        Assert.That(config.TryGetValue("def", out var value), Is.True);
        Assert.That(value, Is.EqualTo("source1"));
        Assert.That(config.GetControllingSourceName("def"), Is.EqualTo("Source 1"));

        config.AddSource(source2);
        config.ApplyChanges();
        Assert.That(config.TryGetValue("def", out value), Is.True);
        Assert.That(value, Is.EqualTo("source2"));
        Assert.That(config.GetControllingSourceName("def"), Is.EqualTo("Source 2"));
    }

    [Test]
    public void KeyChangesInSourceAreDetected()
    {
        config.AddSource(source1);
        config.ApplyChanges();
        Assert.That(config.KeyVersion, Is.EqualTo(1));

        source1["new"] = new("stuff");
        config.ApplyChanges();
        Assert.That(config.KeyVersion, Is.EqualTo(2));
        Assert.That(config.Keys, Is.EquivalentTo(["excl1", "def", "new"]));
        Assert.That(config.TryGetValue("new", out _), Is.True);
    }

    [Test]
    public void GetValueThrows()
    {
        Assert.That(() => config.GetValue("def"), Throws.InstanceOf<KeyNotFoundException>());

        config.AddSource(source1);
        config.ApplyChanges();
        Assert.That(() => config.GetValue("def"), Throws.Nothing);
    }

    [Test]
    public void SetValueOverwritesAllSources()
    {
        config.AddSource(source1);
        config.AddSource(source2);
        config.ApplyChanges();
        Assert.That(config.IsOverwritten("def"), Is.False);

        config.SetValue("def", "overwritten");
        config.ApplyChanges();
        Assert.That(config.TryGetValue("def", out var value), Is.True);
        Assert.That(value, Is.EqualTo("overwritten"));
        Assert.That(config.GetControllingSourceName("def"), Is.EqualTo("Application"));
        Assert.That(config.IsOverwritten("def"), Is.True);

        config.SetValue("def", 3.1);
        config.ApplyChanges();
        Assert.That(config.TryGetValue("def", out value), Is.True);
        Assert.That(value, Is.EqualTo(3.1));
        Assert.That(config.GetControllingSourceName("def"), Is.EqualTo("Application"));
        Assert.That(config.IsOverwritten("def"), Is.True);
    }

    [Test]
    public void ResetValueClearsOverride()
    {
        config.AddSource(source1);
        config.AddSource(source2);
        config.SetValue("def", "overwritten");
        config.ApplyChanges();
        Assert.That(config.IsOverwritten("def"), Is.True);

        config.ResetValue("def");
        config.ApplyChanges();
        Assert.That(config.TryGetValue("def", out var value), Is.True);
        Assert.That(value, Is.EqualTo("source2"));
        Assert.That(config.GetControllingSourceName("def"), Is.EqualTo("Source 2"));
        Assert.That(config.IsOverwritten("def"), Is.False);
    }

    [Test]
    public void BindSectionSetsCurrentValues()
    {
        config.AddSource(source1);
        config.AddSource(source2);
        config.Bind(section);
        config.ApplyChanges();

        Assert.That(section["excl1"], Is.EqualTo(3.0));
        Assert.That(section["excl2"], Is.EqualTo(3.1));
        Assert.That(section["def"], Is.EqualTo("source2"));
        Assert.That(section["exclSection"], Is.EqualTo("section")); // no change, as no source registered that key
    }

    [Test]
    public void BindSectionPropagatesValueChanges()
    {
        config.AddSource(source1);
        config.AddSource(source2);
        config.Bind(section);
        config.ApplyChanges();

        source1["excl1"] = new(3.141592653);
        config.ApplyChanges();
        Assert.That(section["excl1"], Is.EqualTo(3.141592653));
    }

    [Test]
    public void BindSectionPropagatesKeyChanges()
    {
        config.AddSource(source1);
        config.AddSource(source2);
        config.Bind(section);
        config.ApplyChanges();

        source1["exclSection"] = new("source1");
        config.ApplyChanges();
        Assert.That(section["exclSection"], Is.EqualTo("source1"));
    }

    [Test]
    public void UnbindSection()
    {
        config.AddSource(source1);
        var binding = config.Bind(section);
        config.ApplyChanges();

        binding.Dispose();
        source1["excl1"] = new(3.141592653);
        config.ApplyChanges();
        Assert.That(section["excl1"], Is.EqualTo(3.0)); // change not propagated
    }

    [Test]
    public void BindingsAreRefCounted()
    {
        config.AddSource(source1);
        var binding1 = config.Bind(section);
        var binding2 = config.Bind(section);
        config.ApplyChanges();

        binding1.Dispose();
        source1["excl1"] = new(3.141592653);
        config.ApplyChanges();
        Assert.That(section["excl1"], Is.EqualTo(3.141592653)); // refCount still 1

        binding1.Dispose();
        source1["excl1"] = new(42);
        config.ApplyChanges();
        Assert.That(section["excl1"], Is.EqualTo(42)); // refCount still 1, disposing twice is okay

        binding2.Dispose();
        source1["excl1"] = new(1337);
        config.ApplyChanges();
        Assert.That(section["excl1"], Is.EqualTo(42)); // refCount is 0
    }

    [Test]
    public void SectionNotifiesChanges()
    {
        config.AddSource(source1);
        var binding = config.Bind(section);
        config.ApplyChanges();
        Assert.That(config.GetValue("def"), Is.EqualTo("source1"));

        section["def"] = new("section");
        binding.NotifyChanges();
        config.ApplyChanges();
        Assert.That(config.GetValue("def"), Is.EqualTo("section"));
        Assert.That(config.IsOverwritten("def"), Is.True);
    }

    [Test]
    public void SectionToSectionChange()
    {
        var section2 = new InMemoryConfigurationSection(new()
        {
            { "def", new("trash") }
        });

        config.AddSource(source1);
        var binding = config.Bind(section);
        config.Bind(section2);
        config.ApplyChanges();
        Assert.That(section2["def"], Is.EqualTo("source1"));

        section["def"] = new("section");
        binding.NotifyChanges();
        config.ApplyChanges();
        Assert.That(section2["def"], Is.EqualTo("section"));
    }

    [Test]
    public void SectionsAreImplicitlyDefault()
    {
        config.Bind(section);

        config = new(); // with default source
        Assert.That(config.GetControllingSourceName("exclSection"), Is.EqualTo("Default"));
        Assert.That(config.GetValue("exclSection"), Is.EqualTo("section"));
    }
}
