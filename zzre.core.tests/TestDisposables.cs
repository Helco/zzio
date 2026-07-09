using System;
using NUnit.Framework;

namespace zzre.tests;

public sealed class TestDisposables
{
    private class ListDisposableEx : ListDisposable
    {
        public void Add(IDisposable disposable) => AddDisposable(disposable);
    }

    private class CheckDisposal : IDisposable
    {
        public bool WasDisposed;
        public void Dispose()
        {
            Assert.That(WasDisposed, Is.False);
            WasDisposed = true;
        }
    }

    private ListDisposableEx list = null!;

    [SetUp]
    public void SetUp()
    {
        list = new();
    }

    [Test]
    public void DisposeEmpty()
    {
        list.Dispose();
    }

    [Test]
    public void DisposeWithOne()
    {
        var check = new CheckDisposal();
        list.Add(check);
        list.Dispose();
        Assert.That(check.WasDisposed, Is.True);
    }

    [Test]
    public void DisposeTwice()
    {
        var check = new CheckDisposal();
        list.Add(check);
        list.Dispose();
        list.Dispose();
        Assert.That(check.WasDisposed, Is.True);
    }

    [Test]
    public void DisposeMultiple()
    {
        var check1 = new CheckDisposal();
        var check2 = new CheckDisposal();
        list.Add(check1);
        list.Add(check2);
        list.Dispose();
        Assert.That(check1.WasDisposed, Is.True);
        Assert.That(check2.WasDisposed, Is.True);
    }

    [Test]
    public void DisposeNull()
    {
        Assert.That(() =>
        {
            NullDisposable.Instance.Dispose();
            NullDisposable.Instance.Dispose();
            NullDisposable.Instance.Dispose();
        }, Throws.Nothing);
        Assert.That(NullDisposable.Instance, Is.InstanceOf<IDisposable>());
    }
}
