using NUnit.Framework;
using System;
using System.Linq;

namespace zzre.core.tests;

[TestFixture]
public class TestFallbackTagContainer
{
    private class Tag1 { }
    private class Tag2 { }
    private class Tag3 { }
    private class Tag4 { }
    private class SubTag1Of1 : Tag1 { }
    private class SubTag2Of1 : Tag1 { }

    private ITagContainer fallback = null!;
    private ITagContainer main = null!;
    private ITagContainer container = null!;

    [SetUp]
    public void Setup()
    {
        fallback = new TagContainer();
        fallback.AddTag<Tag1>(new SubTag1Of1());
        fallback.AddTag(new Tag2());
        main = new TagContainer();
        main.AddTag(new Tag3());
        main.AddTag(new SubTag2Of1() as Tag1);
        container = main.FallbackTo(fallback);
    }

    [Test]
    public void HasAllTags()
    {
        Assert.That(container.HasTag<Tag1>());
        Assert.That(container.HasTag<Tag2>());
        Assert.That(container.HasTag<Tag3>());
    }

    [Test]
    public void HasOverwrittenTagFromParent()
    {
        Assert.That(container.HasTag<SubTag2Of1>());
        Assert.That(container.GetTag<Tag1>(), Is.SameAs(container.GetTag<SubTag2Of1>()));
    }

    [Test]
    public void GetTagsTakesFromBoth()
    {
        var tags = container.GetTags<Tag1>();
        Assert.That(tags.Count(), Is.EqualTo(2));
        var tagTypes = tags.Select(t => t.GetType());
        Assert.That(tagTypes, Is.EquivalentTo(new Type[] { typeof(SubTag1Of1), typeof(SubTag2Of1) }));
    }

    [Test]
    public void ModifiesMain()
    {
        container.AddTag(new Tag4());
        Assert.True(main.HasTag<Tag4>());
        Assert.True(container.RemoveTag<Tag4>());
        Assert.False(main.HasTag<Tag4>());
    }
}
