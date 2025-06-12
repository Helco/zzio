using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace zzre.tests;

[TestFixture]
public class TestAssetRegistry
{
    private interface ITestAsset : IAsset<TestInfo>
    {
        public IAssetRegistry Registry { get; init; }
        public int Id { get; init; }
    }

    private readonly record struct TestInfo(int Id, Func<IAssetHandle[]>? CreateSecondaries = null) : IEquatable<TestInfo>
    {
        public readonly TaskCompletionSource StartedLoad = new();
        public readonly TaskCompletionSource FinishLoad = new();

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
                new TAsset() { Id = info.Id, Registry = registry },
                info.CreateSecondaries?.Invoke());
        }
    }

    private class GlobalTestAsset : ITestAsset
    {
        public static AssetLocality Locality => AssetLocality.Global;
        public IAssetRegistry Registry { get; init; }
        public int Id { get; init; }

        public static Task<AssetLoadResult<TestInfo>> LoadAsync(IAssetRegistry registry, TestInfo info, CancellationToken ct)
            => TestInfo.LoadAsync<GlobalTestAsset>(registry, info, ct);

        public void Dispose() => Volatile.Write(ref WasDisposed, true);
        public bool WasDisposed;
    }

    private class LocalTestAsset : ITestAsset
    {
        public static AssetLocality Locality => AssetLocality.Local;
        public IAssetRegistry Registry { get; init; }
        public int Id { get; init; }

        public static Task<AssetLoadResult<TestInfo>> LoadAsync(IAssetRegistry registry, TestInfo info, CancellationToken ct)
            => TestInfo.LoadAsync<LocalTestAsset>(registry, info, ct);

        public void Dispose() => Volatile.Write(ref WasDisposed, true);
        public bool WasDisposed;
    }

    private class UniqueTestAsset : ITestAsset
    {
        public static AssetLocality Locality => AssetLocality.Unique;
        public IAssetRegistry Registry { get; init; }
        public int Id { get; init; }

        public static Task<AssetLoadResult<TestInfo>> LoadAsync(IAssetRegistry registry, TestInfo info, CancellationToken ct)
            => TestInfo.LoadAsync<UniqueTestAsset>(registry, info, ct);

        public void Dispose() => Volatile.Write(ref WasDisposed, true);
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
}
