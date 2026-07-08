using System;
using System.Collections.Generic;
using NUnit.Framework;
using NUnit.Framework.Constraints;

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
        set => throw new NotImplementedException();
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

public sealed class TestConfiguration
{
}
