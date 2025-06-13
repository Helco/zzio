using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace zzre.tests;

[TestFixture, CancelAfter(3000), SingleThreaded]
public class TestAssetRegistry
{
    private interface ITestAsset : IAsset<TestInfo>
    {
        public IAssetRegistry Registry { get; init; }
        public TestInfo Info { get; init; }
        public int Id => Info.Id;
    }

    private readonly struct TestInfo(int Id, Func<IAssetHandle[]>? CreateSecondaries = null) : IEquatable<TestInfo>
    {
        public readonly int Id = Id;
        public readonly Func<IAssetHandle[]>? CreateSecondaries = null;
        public readonly TaskCompletionSource StartedLoad = new();
        public readonly TaskCompletionSource FinishLoad = new();
        public readonly TaskCompletionSource Disposed = new();

        public TestInfo(int Id, IAssetHandle[] secondaries) : this(Id, () => secondaries) { }

        public readonly TestInfo AsCompleted()
        {
            FinishLoad.SetResult();
            return this;
        }

        public static async Task<AssetLoadResult<TestInfo>> LoadAsync<TAsset>(IAssetRegistry registry, TestInfo info, CancellationToken ct)
            where TAsset : ITestAsset, new()
        {
            ct.ThrowIfCancellationRequested();
            Assert.That(info.StartedLoad.TrySetResult(), $"Asset {info.Id} was tried to be loaded twice");
            await info.FinishLoad.Task;
            ct.ThrowIfCancellationRequested();
            return new(
                new TAsset() { Info = info, Registry = registry },
                info.CreateSecondaries?.Invoke());
        }

        public bool Equals(TestInfo other) => Id == other.Id;
        public override bool Equals([NotNullWhen(true)] object? obj) => obj is TestInfo other ? Equals(other) : false;
        public override int GetHashCode() => Id.GetHashCode();
    }

    private class GlobalTestAsset : ITestAsset
    {
        public static AssetLocality Locality => AssetLocality.Global;
        public IAssetRegistry Registry { get; init; }
        public TestInfo Info { get; init; }

        public static Task<AssetLoadResult<TestInfo>> LoadAsync(IAssetRegistry registry, TestInfo info, CancellationToken ct)
            => TestInfo.LoadAsync<GlobalTestAsset>(registry, info, ct);

        public void Dispose()
        {
            Volatile.Write(ref WasDisposed, true);
            Info.Disposed.TrySetResult();
        }
        public bool WasDisposed;
    }

    private class GlobalMTDTestAsset : ITestAsset
    {
        public static bool NeedsMainThreadDisposal => true;
        public static AssetLocality Locality => AssetLocality.Global;
        public IAssetRegistry Registry { get; init; }
        public TestInfo Info { get; init; }

        public static Task<AssetLoadResult<TestInfo>> LoadAsync(IAssetRegistry registry, TestInfo info, CancellationToken ct)
            => TestInfo.LoadAsync<GlobalMTDTestAsset>(registry, info, ct);

        public void Dispose()
        {
            Volatile.Write(ref WasDisposed, true);
            Info.Disposed.TrySetResult();
        }
        public bool WasDisposed;
    }

    private class LocalTestAsset : ITestAsset
    {
        public static AssetLocality Locality => AssetLocality.Local;
        public IAssetRegistry Registry { get; init; }
        public TestInfo Info { get; init; }

        public static Task<AssetLoadResult<TestInfo>> LoadAsync(IAssetRegistry registry, TestInfo info, CancellationToken ct)
            => TestInfo.LoadAsync<LocalTestAsset>(registry, info, ct);

        public void Dispose()
        {
            Volatile.Write(ref WasDisposed, true);
            Info.Disposed.TrySetResult();
        }
        public bool WasDisposed;
    }

    private class UniqueTestAsset : ITestAsset
    {
        public static AssetLocality Locality => AssetLocality.Unique;
        public IAssetRegistry Registry { get; init; }
        public TestInfo Info { get; init; }

        public static Task<AssetLoadResult<TestInfo>> LoadAsync(IAssetRegistry registry, TestInfo info, CancellationToken ct)
            => TestInfo.LoadAsync<UniqueTestAsset>(registry, info, ct);

        public void Dispose()
        {
            Volatile.Write(ref WasDisposed, true);
            Info.Disposed.TrySetResult();
        }
        public bool WasDisposed;
    }

    private readonly TagContainer DI = new();

    [Test]
    public void EmptyRegistries()
    {
        using var global = new AssetRegistry(DI);
        using var local = new AssetRegistry(DI, global);
        using var local2 = new AssetRegistry(DI, global);

        Assert.That(global.IsLocalRegistry, Is.False);
        Assert.That(local.IsLocalRegistry, Is.True);
        Assert.That(local2.IsLocalRegistry, Is.True);
    }

    [Test]
    public void CannotTwiceLocal()
    {
        using var global = new AssetRegistry(DI);
        using var local = new AssetRegistry(DI, global);

        Assert.That(() => new AssetRegistry(DI, local), Throws.Exception);
    }

    [Test]
    public void RegistryDisposeOrder()
    {
        var global = new AssetRegistry(DI);
        var local = new AssetRegistry(DI, global);
        local.Dispose();
        global.Dispose();

        global = new AssetRegistry(DI);
        local = new AssetRegistry(DI, global);
        global.Dispose();
        local.Dispose();
    }

    private void CommonAssetChecks<TAsset>(IAssetRegistry registry, AssetHandle<TAsset> handle, int id)
    where TAsset : class, ITestAsset => Assert.Multiple(() =>
    {
        Assert.That(handle.Asset, Is.Not.Null);
        Assert.That(handle.Asset.Id, Is.EqualTo(id));
        Assert.That(handle.Asset.Registry, Is.SameAs(registry));
        Assert.That(handle.Registry, Is.SameAs(registry));
    });

    [Test]
    public void LoadSyncGlobal_Single()
    {
        using var global = new AssetRegistry(DI);
        using var handle = global.Load<TestInfo, GlobalTestAsset>(new TestInfo(1).AsCompleted(), AssetPriority.Synchronous);
        CommonAssetChecks(global, handle, 1);
    }

    [Test]
    public void LoadSyncGlobal_MultipleDiff()
    {
        using var global = new AssetRegistry(DI);
        using var handle1 = global.Load<TestInfo, GlobalTestAsset>(new TestInfo(1).AsCompleted(), AssetPriority.Synchronous);
        using var handle2 = global.Load<TestInfo, GlobalTestAsset>(new TestInfo(42).AsCompleted(), AssetPriority.Synchronous);
        using var handle3 = global.Load<TestInfo, GlobalTestAsset>(new TestInfo(1337).AsCompleted(), AssetPriority.Synchronous);
        CommonAssetChecks(global, handle1, 1);
        CommonAssetChecks(global, handle2, 42);
        CommonAssetChecks(global, handle3, 1337);
    }

    [Test]
    public void LoadSyncGlobal_MultipleSame()
    {
        using var global = new AssetRegistry(DI);
        using var handle1 = global.Load<TestInfo, GlobalTestAsset>(new TestInfo(1).AsCompleted(), AssetPriority.Synchronous);
        using var handle2 = global.Load<TestInfo, GlobalTestAsset>(new TestInfo(1).AsCompleted(), AssetPriority.Synchronous);
        using var handle3 = global.Load<TestInfo, GlobalTestAsset>(new TestInfo(1).AsCompleted(), AssetPriority.Synchronous);
        CommonAssetChecks(global, handle1, 1);
        CommonAssetChecks(global, handle2, 1);
        CommonAssetChecks(global, handle3, 1);

        Assert.That(handle1, Is.EqualTo(handle2));
        Assert.That(handle1, Is.EqualTo(handle3));
        Assert.That(handle1.Asset, Is.SameAs(handle2.Asset));
        Assert.That(handle1.Asset, Is.SameAs(handle3.Asset));
    }

    [Test]
    public void DisposeAsset_MultiSyncRefs()
    {
        using var global = new AssetRegistry(DI);
        var handle1 = global.Load<TestInfo, GlobalTestAsset>(new TestInfo(1).AsCompleted(), AssetPriority.Synchronous);
        var handle2 = global.Load<TestInfo, GlobalTestAsset>(new TestInfo(1).AsCompleted(), AssetPriority.Synchronous);
        var handle3 = global.Load<TestInfo, GlobalTestAsset>(new TestInfo(1).AsCompleted(), AssetPriority.Synchronous);
        var asset = handle1.Asset;

        Assert.That(asset.WasDisposed, Is.False);
        handle1.Dispose();
        Assert.That(asset.WasDisposed, Is.False);
        handle3.Dispose();
        Assert.That(asset.WasDisposed, Is.False);
        handle2.Dispose();
        Assert.That(asset.WasDisposed, Is.True);
    }

    [Test]
    public async Task DisposeAsset_MultiThreaded([Values] bool needsMainThread, [Values] bool isMainThread, CancellationToken ct)
    {
        using var global = new AssetRegistry(DI);

        IAssetHandle handle;
        ITestAsset asset;
        if (needsMainThread)
        {
            var thandle = global.Load<TestInfo, GlobalMTDTestAsset>(new TestInfo(1).AsCompleted(), AssetPriority.Synchronous);
            handle = thandle;
            asset = thandle.Asset!;
        }
        else
        {
            var thandle = global.Load<TestInfo, GlobalTestAsset>(new TestInfo(1).AsCompleted(), AssetPriority.Synchronous);
            handle = thandle;
            asset = thandle.Asset!;
        }
        Assert.That(asset.Info.Disposed.Task.IsCompleted, Is.False);

        if (isMainThread)
        {
            handle.Dispose();
            Assert.That(asset.Info.Disposed.Task.IsCompletedSuccessfully, Is.True);
        }
        else if (needsMainThread)
        {
            await Task.Run(handle.Dispose);
            Assert.That(asset.Info.Disposed.Task.IsCompleted, Is.False);
            global.Update();
            Assert.That(asset.Info.Disposed.Task.IsCompletedSuccessfully, Is.True);
        }
        else
        {
            await Task.Run(() =>
            {
                handle.Dispose();
                Assert.That(asset.Info.Disposed.Task.IsCompletedSuccessfully, Is.True);
            }, ct);
        }
    }
}
