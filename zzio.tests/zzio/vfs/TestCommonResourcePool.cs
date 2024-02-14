using NUnit.Framework;
using System;
using System.Linq;
using zzio.vfs;

namespace zzio.tests.vfs;

[TestFixture]
public class TestCommonResourcePool
{
    public static IResourcePool[] testPools = PoolResources.AllResourcePools;

    private static void VisitResources(IResourcePool pool, Action<IResource> action)
    {
        void visit(IResource res)
        {
            action(res);
            foreach (var child in res)
                visit(child);
        }
        visit(pool.Root);
    }

    [Test, Combinatorial]
    public void rootIsDirectory([ValueSource(nameof(testPools))] IResourcePool pool)
    {
        Assert.That(pool.Root.Type, Is.EqualTo(ResourceType.Directory));
    }

    [Test, Combinatorial]
    public void rootHasNoParent([ValueSource(nameof(testPools))] IResourcePool pool)
    {
        Assert.That(pool.Root.Parent, Is.Null);
    }

    [Test, Combinatorial]
    public void rootHasEmptyPath([ValueSource(nameof(testPools))] IResourcePool pool)
    {
        Assert.That(pool.Root.Path.ToString(), Is.EqualTo(""));
    }

    [Test, Combinatorial]
    public void poolIsAlwaysSet([ValueSource(nameof(testPools))] IResourcePool pool) => VisitResources(pool, res =>
    {
        Assert.That(res.Pool, Is.Not.Null);
    });

    [Test, Combinatorial]
    public void enumeratorVsSplit([ValueSource(nameof(testPools))] IResourcePool pool) => VisitResources(pool, res =>
    {
        MyAssert.ContainsExactly(res, res.Files.Concat(res.Directories));
    });

    [Test, Combinatorial]
    public void resourcesTypesInSplit([ValueSource(nameof(testPools))] IResourcePool pool) => VisitResources(pool, res =>
    {
        foreach (var file in res.Files)
            Assert.That(file.Type, Is.EqualTo(ResourceType.File));
        foreach (var dir in res.Directories)
            Assert.That(dir.Type, Is.EqualTo(ResourceType.Directory));
    });

    [Test, Combinatorial]
    public void parentIsSetCorrectly([ValueSource(nameof(testPools))] IResourcePool pool) => VisitResources(pool, res =>
    {
        foreach (var child in res)
            Assert.That(child.Parent, Is.EqualTo(res));
    });

    [Test, Combinatorial]
    public void openContent([ValueSource(nameof(testPools))] IResourcePool pool) => VisitResources(pool, res =>
    {
        using var stream = res.OpenContent();
        if (res.Type == ResourceType.File)
        {
            Assert.That(stream, Is.Not.Null);
            Assert.That(stream!.CanRead);
        }
        else
            Assert.That(stream, Is.Null);
    });

    [Test, Combinatorial]
    public void filesHaveNoChildren([ValueSource(nameof(testPools))] IResourcePool pool) => VisitResources(pool, res =>
    {
        if (res.Type == ResourceType.File)
        {
            Assert.That(res, Is.Empty);
        }
    });
}
