using NUnit.Framework;
using System;
using System.Linq;

namespace zzre.tests;

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

    private class DisposalTest<T> : IDisposable
    {
        public bool IsDisposed { get; private set; }
        public void Dispose() => IsDisposed = true;
    }

    [Test]
    public void DisposeOnlyDisposesOwnTags()
    {
        var parentInt = new DisposalTest<int>();
        var parentBool = new DisposalTest<bool>();
        var extensionBool = new DisposalTest<bool>();
        using var parent = new TagContainer();
        parent.AddTag(parentInt);
        parent.AddTag(parentBool);

        using var extension = new ExtendedTagContainer(parent);
        extension.AddTag(extensionBool);
        extension.Dispose();

        Assert.That(extensionBool.IsDisposed, Is.True);
        Assert.That(parentBool.IsDisposed, Is.False);
        Assert.That(parentInt.IsDisposed, Is.False);
    }

    [Test]
    public void TryGetTagPrefersExtension()
    {
        using var parent = new TagContainer();
        parent.AddTag(new Tag1());
        parent.AddTag(new Tag2());
        using var extension = new ExtendedTagContainer(parent);
        var extensionTag2 = new Tag2();
        extension.AddTag(extensionTag2);
        extension.AddTag(new Tag3());

        Assert.That(extension.TryGetTag<Tag1>(out _), Is.True);
        Assert.That(extension.TryGetTag<Tag2>(out var actualTag2), Is.True);
        Assert.That(actualTag2, Is.SameAs(extensionTag2));
        Assert.That(extension.TryGetTag<Tag3>(out _), Is.True);
        Assert.That(parent.TryGetTag<Tag3>(out _), Is.False);
    }

    [Test]
    public void GetTagPrefersExtension()
    {
        using var parent = new TagContainer();
        parent.AddTag(new Tag1());
        parent.AddTag(new Tag2());
        using var extension = new ExtendedTagContainer(parent);
        var extensionTag2 = new Tag2();
        extension.AddTag(extensionTag2);
        extension.AddTag(new Tag3());

        Assert.That(() => extension.GetTag<Tag1>(), Throws.Nothing);
        Assert.That(extension.GetTag<Tag2>(), Is.SameAs(extensionTag2));
        Assert.That(() => extension.GetTag<Tag3>(), Throws.Nothing);
    }
}
