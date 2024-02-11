using NUnit.Framework;
using System;
using System.Linq;

namespace zzre.core.tests;

[TestFixture]
public class TestExtendedTagContainer
{
    private class Tag1 { }
    private class Tag2 { }
    private class Tag3 { }
    private class Tag4 { }
    private class SubTag1Of1 : Tag1 { }
    private class SubTag2Of1 : Tag1 { }

    private ITagContainer parentContainer = null!;
    private ITagContainer container = null!;

    [SetUp]
    public void Setup()
    {
        parentContainer = new TagContainer();
        parentContainer.AddTag<Tag1>(new SubTag1Of1());
        parentContainer.AddTag(new Tag2());
        container = parentContainer.ExtendedWith(new Tag3(), new SubTag2Of1() as Tag1);
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
    public void AddTagDoesNotModifyParent()
    {
        container.AddTag(new Tag4());
        Assert.That(container.HasTag<Tag4>());
        Assert.That(parentContainer.HasTag<Tag4>(), Is.False);
    }

    [Test]
    public void RemoveTagDoesNotModifyParent()
    {
        Assert.That(container.RemoveTag<Tag2>(), Is.False);
        Assert.That(parentContainer.HasTag<Tag2>());
    }
}
