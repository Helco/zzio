using NUnit.Framework;
using System;

namespace zzre.tests;

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

    [Test]
    public void CannotAddTagTwice()
    {
        container.AddTag(new Tag1());
        Assert.That(() => container.AddTag(new Tag1()), Throws.ArgumentException);
        Assert.That(() => container.AddTag<Tag1>(new SubTag1Of1()), Throws.ArgumentException);
    }

    private class DisposalTest<T> : IDisposable
    {
        public bool IsDisposed { get; private set; }
        public void Dispose() => IsDisposed = true;
    }

    [Test]
    public void DisposeDisposesTags()
    {
        var intTag = new DisposalTest<int>();
        var boolTag = new DisposalTest<bool>();
        container.AddTag(new Tag1());
        container.AddTag(intTag);
        container.AddTag(boolTag);
        container.Dispose();

        Assert.That(intTag.IsDisposed, Is.True);
        Assert.That(boolTag.IsDisposed, Is.True);
    }

    [Test]
    public void RemoveTagDisposesIfWanted()
    {
        var intTag = new DisposalTest<int>();
        var boolTag = new DisposalTest<bool>();
        var floatTag = new DisposalTest<float>();
        container.AddTag(intTag);
        container.AddTag(boolTag);
        container.AddTag(floatTag);
        container.RemoveTag<DisposalTest<int>>(dispose: true);
        container.RemoveTag<DisposalTest<bool>>(dispose: false);
        container.RemoveTag<DisposalTest<float>>(); // testing default

        Assert.That(intTag.IsDisposed, Is.True);
        Assert.That(boolTag.IsDisposed, Is.False);
        Assert.That(floatTag.IsDisposed, Is.True);
    }
}
