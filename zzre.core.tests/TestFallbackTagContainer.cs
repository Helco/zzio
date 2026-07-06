using NUnit.Framework;
using System;
using System.Linq;

namespace zzre.tests;

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
        Assert.That(main.HasTag<Tag4>());
        Assert.That(container.RemoveTag<Tag4>());
        Assert.That(main.HasTag<Tag4>(), Is.False);
    }

    [Test]
    public void GetTagPrefersMain()
    {
        Assert.That(container.GetTag<Tag1>(), Is.SameAs(main.GetTag<Tag1>()));
        Assert.That(container.GetTag<Tag2>(), Is.SameAs(fallback.GetTag<Tag2>()));
        Assert.That(container.GetTag<Tag3>(), Is.SameAs(main.GetTag<Tag3>()));
    }

    [Test]
    public void TryGetTagPrefersMain()
    {
        Assert.That(container.TryGetTag<Tag1>(out var tag1), Is.True);
        Assert.That(container.TryGetTag<Tag2>(out var tag2), Is.True);
        Assert.That(container.TryGetTag<Tag3>(out var tag3), Is.True);
        Assert.That(tag1, Is.SameAs(main.GetTag<Tag1>()));
        Assert.That(tag2, Is.SameAs(fallback.GetTag<Tag2>()));
        Assert.That(tag3, Is.SameAs(main.GetTag<Tag3>()));
    }
}
