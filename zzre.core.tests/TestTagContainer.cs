using NUnit.Framework;
using System;

namespace zzre.core.tests;

[TestFixture]
public class TestTagContainer
{
    private class Tag1 { }
    private class SubTag1Of1 : Tag1 { }
    private class SubTag2Of1 : Tag1 { }

    private ITagContainer container = null!;

    [SetUp]
    public void Setup()
    {
        container = new TagContainer();
    }

    [Test]
    public void StartsEmpty()
    {
        Assert.That(container.GetTags<object>(), Is.Empty);
    }

    [Test]
    public void GetTagThrows()
    {
        Assert.That(() => container.GetTag<Tag1>(), Throws.InstanceOf<Exception>());
    }

    [Test]
    public void CanAddAndRemoveNewTag()
    {
        Assert.That(container.HasTag<Tag1>(), Is.False);
        Assert.That(container.RemoveTag<Tag1>(), Is.False);
        container.AddTag(new Tag1());
        Assert.That(container.HasTag<Tag1>());
        Assert.That(container.RemoveTag<Tag1>());
        Assert.That(container.HasTag<Tag1>(), Is.False);
        Assert.That(container.RemoveTag<Tag1>(), Is.False);
    }
}
