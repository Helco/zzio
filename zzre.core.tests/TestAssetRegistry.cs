using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using NUnit.Framework.Constraints;

namespace zzre.tests;

[TestFixture(TaskContinuationOptions.None)]
[TestFixture(TaskContinuationOptions.RunContinuationsAsynchronously)]
[TestFixture(TaskContinuationOptions.ExecuteSynchronously)]
[CancelAfter(3000), SingleThreaded]
public class TestAssetRegistry
{
    private interface ITestAsset : IAsset<TestInfo>
    {
        public IAssetRegistry Registry { get; init; }
        public TestInfo Info { get; init; }
        public int Id => Info.Id;
    }

    private readonly struct TestInfo(TaskContinuationOptions tcsOptions,
        int Id, Func<IAssetHandle[]>? CreateSecondaries = null) : IEquatable<TestInfo>
    {
        public readonly int Id = Id;
        public readonly Func<IAssetHandle[]>? CreateSecondaries = CreateSecondaries;
        public readonly TaskCompletionSource StartedLoad = new(tcsOptions);
        public readonly TaskCompletionSource FinishLoad = new(tcsOptions);
        public readonly TaskCompletionSource Disposed = new(tcsOptions);

        public TestInfo(TaskContinuationOptions tcsOptions, int Id, IAssetHandle[] secondaries)
            : this(tcsOptions, Id, () => secondaries) { }

        public readonly TestInfo AsCompleted()
        {
            FinishLoad.SetResult();
            return this;
        }

        public readonly TestInfo AsErroneous()
        {
            FinishLoad.SetException(new TestException());
            return this;
        }

        public static async Task<AssetLoadResult<TestInfo>> LoadAsync<TAsset>(IAssetRegistry registry, TestInfo info, CancellationToken ct)
            where TAsset : ITestAsset, new()
        {
            ct.ThrowIfCancellationRequested();
            Assert.That(info.StartedLoad.TrySetResult(), $"Asset {info.Id} was tried to be loaded twice");
            await info.FinishLoad.Task.WaitAsync(ct);
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
    private readonly TaskContinuationOptions tcsOptions;

    public TestAssetRegistry(TaskContinuationOptions tcsOptions) =>
        this.tcsOptions = tcsOptions;

    private TestInfo GetInfo(int id, Func<IAssetHandle[]>? createSecondaries = null) =>
        new TestInfo(tcsOptions, id, createSecondaries);

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

        global = new AssetRegistry(DI);
        local = new AssetRegistry(DI, global);
        var local2 = new AssetRegistry(DI, global);
        local.Dispose();
        global.Dispose();
        local2.Dispose();
    }

    private void CommonAssetChecks<TAsset>(IAssetRegistry registry, AssetHandle<TAsset> handle, int id, TAsset? extAsset = null)
    where TAsset : class, ITestAsset => Assert.Multiple(() =>
    {
        if (extAsset is not null)
            Assert.That(handle.Asset, Is.SameAs(extAsset));
        Assert.That(handle.Asset, Is.Not.Null);
        Assert.That(handle.Asset.Id, Is.EqualTo(id));
        Assert.That(handle.Asset.Registry, Is.SameAs(registry));
        Assert.That(handle.Registry, Is.SameAs(registry));
    });

    [Test]
    public void LoadSync_Single()
    {
        using var global = new AssetRegistry(DI);
        using var handle = global.Load<TestInfo, GlobalTestAsset>(GetInfo(1).AsCompleted(), AssetPriority.Synchronous);
        CommonAssetChecks(global, handle, 1);
    }

    [Test]
    public void LoadSync_MultipleDiff()
    {
        using var global = new AssetRegistry(DI);
        using var handle1 = global.Load<TestInfo, GlobalTestAsset>(GetInfo(1).AsCompleted(), AssetPriority.Synchronous);
        using var handle2 = global.Load<TestInfo, GlobalTestAsset>(GetInfo(42).AsCompleted(), AssetPriority.Synchronous);
        using var handle3 = global.Load<TestInfo, GlobalTestAsset>(GetInfo(1337).AsCompleted(), AssetPriority.Synchronous);
        CommonAssetChecks(global, handle1, 1);
        CommonAssetChecks(global, handle2, 42);
        CommonAssetChecks(global, handle3, 1337);
    }

    [Test]
    public void LoadSync_MultipleSame()
    {
        using var global = new AssetRegistry(DI);
        using var handle1 = global.Load<TestInfo, GlobalTestAsset>(GetInfo(1).AsCompleted(), AssetPriority.Synchronous);
        using var handle2 = global.Load<TestInfo, GlobalTestAsset>(GetInfo(1).AsCompleted(), AssetPriority.Synchronous);
        using var handle3 = global.Load<TestInfo, GlobalTestAsset>(GetInfo(1).AsCompleted(), AssetPriority.Synchronous);
        CommonAssetChecks(global, handle1, 1);
        CommonAssetChecks(global, handle2, 1);
        CommonAssetChecks(global, handle3, 1);

        Assert.That(handle1, Is.EqualTo(handle2));
        Assert.That(handle1, Is.EqualTo(handle3));
        Assert.That(handle1.Asset, Is.SameAs(handle2.Asset));
        Assert.That(handle1.Asset, Is.SameAs(handle3.Asset));
    }

    [Test]
    public async Task LoadHigh_Single(CancellationToken ct)
    {
        using var global = new AssetRegistry(DI);
        var info = GetInfo(1); // uncompleted
        using var handle = global.Load<TestInfo, GlobalTestAsset>(info, AssetPriority.High);

        Assert.That(handle.Asset, Is.Null);
        await info.StartedLoad.Task.WaitAsync(ct);
        Assert.That(handle.Asset, Is.Null);
        info.FinishLoad.SetResult();
        var asset = await handle.GetAsync(ct);
        CommonAssetChecks(global, handle, 1, asset);
    }

    [Test]
    public async Task LoadHigh_MultipleDiff(CancellationToken ct)
    {
        using var global = new AssetRegistry(DI);
        var info1 = GetInfo(1);
        var info2 = GetInfo(2);
        var info3 = GetInfo(3);
        using var handle1 = global.Load<TestInfo, GlobalTestAsset>(info1, AssetPriority.High);
        using var handle2 = global.Load<TestInfo, GlobalTestAsset>(info2, AssetPriority.High);
        using var handle3 = global.Load<TestInfo, GlobalTestAsset>(info3, AssetPriority.High);

        Assert.That(handle1.Asset, Is.Null);
        Assert.That(handle2.Asset, Is.Null);
        Assert.That(handle3.Asset, Is.Null);
        info2.FinishLoad.SetResult();
        var asset2 = await handle2.GetAsync(ct);
        CommonAssetChecks(global, handle2, 2, asset2);

        Assert.That(handle1.Asset, Is.Null);
        Assert.That(handle2.Asset, Is.Not.Null);
        Assert.That(handle3.Asset, Is.Null);
        info1.FinishLoad.SetResult();
        var asset1 = await handle1.GetAsync(ct);
        CommonAssetChecks(global, handle1, 1, asset1);

        Assert.That(handle1.Asset, Is.Not.Null);
        Assert.That(handle2.Asset, Is.Not.Null);
        Assert.That(handle3.Asset, Is.Null);
        info3.FinishLoad.SetResult();
        var asset3 = await handle3.GetAsync(ct);
        CommonAssetChecks(global, handle3, 3, asset3);
    }

    [Test]
    public async Task LoadHigh_MultipleSame_Parallel1(CancellationToken ct)
    {
        using var global = new AssetRegistry(DI);
        var info = GetInfo(1);
        using var handle1 = global.Load<TestInfo, GlobalTestAsset>(info, AssetPriority.High);
        using var handle2 = global.Load<TestInfo, GlobalTestAsset>(info, AssetPriority.High);
        using var handle3 = global.Load<TestInfo, GlobalTestAsset>(info, AssetPriority.High);

        Assert.That(handle1.Asset, Is.Null);
        Assert.That(handle2.Asset, Is.Null);
        Assert.That(handle3.Asset, Is.Null);
        info.FinishLoad.SetResult();

        var asset2 = await handle2.GetAsync(ct);
        CommonAssetChecks(global, handle2, 1, asset2);
        Assert.That(handle1.Asset, Is.SameAs(asset2));
        Assert.That(handle3.Asset, Is.SameAs(asset2));

        var asset1 = await handle1.GetAsync(ct);
        var asset3 = await handle1.GetAsync(ct);
        CommonAssetChecks(global, handle1, 1, asset1);
        CommonAssetChecks(global, handle3, 1, asset3);
    }

    [Test]
    public async Task LoadHigh_MultipleSame_Parallel2(CancellationToken ct)
    {
        using var global = new AssetRegistry(DI);
        var info = GetInfo(1);
        using var handle1 = global.Load<TestInfo, GlobalTestAsset>(info, AssetPriority.High);
        using var handle2 = global.Load<TestInfo, GlobalTestAsset>(info, AssetPriority.High);
        using var handle3 = global.Load<TestInfo, GlobalTestAsset>(info, AssetPriority.High);

        var waitTask = Task.WhenAll(
            Task.Run(() => handle1.GetAsync(ct).AsTask(), ct),
            Task.Run(() => handle2.GetAsync(ct).AsTask(), ct),
            Task.Run(() => handle3.GetAsync(ct).AsTask(), ct));
        info.FinishLoad.SetResult();
        var results = await waitTask.WaitAsync(ct);

        CommonAssetChecks(global, handle1, 1, results[0]);
        CommonAssetChecks(global, handle1, 1, results[1]);
        CommonAssetChecks(global, handle1, 1, results[2]);
    }

    [Test]
    public async Task LoadHigh_MultipleSame_Sequential(CancellationToken ct)
    {
        using var global = new AssetRegistry(DI);
        var info = GetInfo(1).AsCompleted();

        using var handle1 = global.Load<TestInfo, GlobalTestAsset>(info, AssetPriority.High);
        var asset1 = await handle1.GetAsync(ct);
        CommonAssetChecks(global, handle1, 1, asset1);

        using var handle2 = global.Load<TestInfo, GlobalTestAsset>(info, AssetPriority.High);
        var asset2 = await handle1.GetAsync(ct);
        CommonAssetChecks(global, handle2, 1, asset2);
        Assert.That(asset1, Is.SameAs(asset2));
    }

    [Test]
    public async Task LoadHigh_MultipleSame_Interleaved(CancellationToken ct)
    {
        using var global = new AssetRegistry(DI);
        var info = GetInfo(1);

        using var handle1 = global.Load<TestInfo, GlobalTestAsset>(info, AssetPriority.High);
        await info.StartedLoad.Task.WaitAsync(ct);
        using var handle2 = global.Load<TestInfo, GlobalTestAsset>(info, AssetPriority.High);
        info.FinishLoad.SetResult();

        var asset2 = await handle2.GetAsync(ct);
        var asset1 = await handle1.GetAsync(ct);
        CommonAssetChecks(global, handle1, 1, asset1);
        CommonAssetChecks(global, handle2, 1, asset2);
        Assert.That(asset1, Is.SameAs(asset2));
    }

    [Test]
    public void LoadHigh_AccessSync()
    {
        using var global = new AssetRegistry(DI);
        var info = GetInfo(1);
        using var handle = global.Load<TestInfo, GlobalTestAsset>(info, AssetPriority.High);
        info.FinishLoad.SetResult();

        var asset = handle.Get();
        CommonAssetChecks(global, handle, 1, asset);
    }

    [Test]
    public async Task LoadHigh_ThenLoadSync(CancellationToken ct)
    {
        using var global = new AssetRegistry(DI);
        var info = GetInfo(1);
        using var handle1 = global.Load<TestInfo, GlobalTestAsset>(info, AssetPriority.High);

        await info.StartedLoad.Task.WaitAsync(ct);
        info.FinishLoad.SetResult();
        using var handle2 = global.Load<TestInfo, GlobalTestAsset>(info, AssetPriority.Synchronous);
        var asset2 = handle2.Get();

        CommonAssetChecks(global, handle2, 1, asset2);
    }

    [Test]
    public async Task LoadLow_Single(CancellationToken ct)
    {
        using var global = new AssetRegistry(DI);
        var info = GetInfo(1); // uncompleted
        using var handle = global.Load<TestInfo, GlobalTestAsset>(info, AssetPriority.Low);

        Assert.That(handle.Asset, Is.Null);
        global.Update();
        await info.StartedLoad.Task.WaitAsync(ct);
        Assert.That(handle.Asset, Is.Null);
        info.FinishLoad.SetResult();
        var asset = await handle.GetAsync(ct);
        CommonAssetChecks(global, handle, 1, asset);
    }

    [Test]
    public async Task LoadLow_MultipleDiff(CancellationToken ct)
    {
        using var global = new AssetRegistry(DI);
        var info1 = GetInfo(1);
        var info2 = GetInfo(2);
        var info3 = GetInfo(3);
        using var handle1 = global.Load<TestInfo, GlobalTestAsset>(info1, AssetPriority.Low);
        using var handle2 = global.Load<TestInfo, GlobalTestAsset>(info2, AssetPriority.Low);
        using var handle3 = global.Load<TestInfo, GlobalTestAsset>(info3, AssetPriority.Low);

        Assert.That(handle1.Asset, Is.Null);
        Assert.That(handle2.Asset, Is.Null);
        Assert.That(handle3.Asset, Is.Null);
        global.Update();
        info2.FinishLoad.SetResult();
        var asset2 = await handle2.GetAsync(ct);
        CommonAssetChecks(global, handle2, 2, asset2);

        Assert.That(handle1.Asset, Is.Null);
        Assert.That(handle2.Asset, Is.Not.Null);
        Assert.That(handle3.Asset, Is.Null);
        info1.FinishLoad.SetResult();
        var asset1 = await handle1.GetAsync(ct);
        CommonAssetChecks(global, handle1, 1, asset1);

        Assert.That(handle1.Asset, Is.Not.Null);
        Assert.That(handle2.Asset, Is.Not.Null);
        Assert.That(handle3.Asset, Is.Null);
        info3.FinishLoad.SetResult();
        var asset3 = await handle3.GetAsync(ct);
        CommonAssetChecks(global, handle3, 3, asset3);
    }

    [Test]
    public async Task LoadLow_MultipleSame_Parallel1(CancellationToken ct)
    {
        using var global = new AssetRegistry(DI);
        var info = GetInfo(1);
        using var handle1 = global.Load<TestInfo, GlobalTestAsset>(info, AssetPriority.Low);
        using var handle2 = global.Load<TestInfo, GlobalTestAsset>(info, AssetPriority.Low);
        using var handle3 = global.Load<TestInfo, GlobalTestAsset>(info, AssetPriority.Low);

        Assert.That(handle1.Asset, Is.Null);
        Assert.That(handle2.Asset, Is.Null);
        Assert.That(handle3.Asset, Is.Null);
        global.Update();
        info.FinishLoad.SetResult();

        var asset2 = await handle2.GetAsync(ct);
        CommonAssetChecks(global, handle2, 1, asset2);
        Assert.That(handle1.Asset, Is.SameAs(asset2));
        Assert.That(handle3.Asset, Is.SameAs(asset2));

        var asset1 = await handle1.GetAsync(ct);
        var asset3 = await handle1.GetAsync(ct);
        CommonAssetChecks(global, handle1, 1, asset1);
        CommonAssetChecks(global, handle3, 1, asset3);
    }

    [Test]
    public async Task LoadLow_MultipleSame_Parallel2(CancellationToken ct)
    {
        using var global = new AssetRegistry(DI);
        var info = GetInfo(1);
        using var handle1 = global.Load<TestInfo, GlobalTestAsset>(info, AssetPriority.Low);
        using var handle2 = global.Load<TestInfo, GlobalTestAsset>(info, AssetPriority.Low);
        using var handle3 = global.Load<TestInfo, GlobalTestAsset>(info, AssetPriority.Low);

        var waitTask = Task.WhenAll(
            Task.Run(() => handle1.GetAsync(ct).AsTask(), ct),
            Task.Run(() => handle2.GetAsync(ct).AsTask(), ct),
            Task.Run(() => handle3.GetAsync(ct).AsTask(), ct));
        global.Update();
        info.FinishLoad.SetResult();
        var results = await waitTask.WaitAsync(ct);

        CommonAssetChecks(global, handle1, 1, results[0]);
        CommonAssetChecks(global, handle1, 1, results[1]);
        CommonAssetChecks(global, handle1, 1, results[2]);
    }

    [Test]
    public async Task LoadLow_MultipleSame_Sequential(CancellationToken ct)
    {
        using var global = new AssetRegistry(DI);
        var info = GetInfo(1).AsCompleted();

        using var handle1 = global.Load<TestInfo, GlobalTestAsset>(info, AssetPriority.Low);
        global.Update();
        var asset1 = await handle1.GetAsync(ct);
        CommonAssetChecks(global, handle1, 1, asset1);

        using var handle2 = global.Load<TestInfo, GlobalTestAsset>(info, AssetPriority.Low);
        global.Update(); // should be noop
        var asset2 = await handle1.GetAsync(ct);
        CommonAssetChecks(global, handle2, 1, asset2);
        Assert.That(asset1, Is.SameAs(asset2));
    }

    [Test]
    public async Task LoadLow_MultipleSame_Interleaved(CancellationToken ct)
    {
        using var global = new AssetRegistry(DI);
        var info = GetInfo(1);

        using var handle1 = global.Load<TestInfo, GlobalTestAsset>(info, AssetPriority.Low);
        global.Update();
        await info.StartedLoad.Task.WaitAsync(ct);
        using var handle2 = global.Load<TestInfo, GlobalTestAsset>(info, AssetPriority.Low);
        info.FinishLoad.SetResult();

        var asset2 = await handle2.GetAsync(ct);
        var asset1 = await handle1.GetAsync(ct);
        CommonAssetChecks(global, handle1, 1, asset1);
        CommonAssetChecks(global, handle2, 1, asset2);
        Assert.That(asset1, Is.SameAs(asset2));
    }

    [Test]
    public async Task LoadLow_DelRefBeforeLoad(CancellationToken ct)
    {
        using var global = new AssetRegistry(DI);
        var info = GetInfo(1);

        using var handle1 = global.Load<TestInfo, GlobalTestAsset>(info, AssetPriority.High);
        using var handle2 = global.Load<TestInfo, GlobalTestAsset>(info, AssetPriority.Low);

        info.FinishLoad.SetResult();
        var asset1 = await handle1.GetAsync(ct);
        handle1.Dispose();
        global.Update();

        Assert.That(handle1.Get, Throws.InstanceOf<ObjectDisposedException>());
        Assert.That(asset1.WasDisposed, Is.False);
        var asset2 = await handle2.GetAsync(ct);
        Assert.That(asset1, Is.SameAs(asset2));
        CommonAssetChecks(global, handle2, 1, asset2);
    }

    [Test]
    public async Task LoadSequential([Values] AssetPriority prio1, [Values] AssetPriority prio2, CancellationToken ct)
    {
        using var global = new AssetRegistry(DI);

        var (handle1, asset1) = await CommonLoadAsset1(global, prio1, ct);
        var (handle2, asset2) = await CommonLoadAsset1(global, prio2, ct);
        CommonAssetChecks(global, handle1, 1, asset1);
        CommonAssetChecks(global, handle2, 1, asset2);
    }

    [Test]
    public async Task LoadSequential_WithDisposal([Values] AssetPriority prio1, [Values] AssetPriority prio2, CancellationToken ct)
    {
        using var global = new AssetRegistry(DI);

        var (handle1, asset1) = await CommonLoadAsset1(global, prio1, ct);
        CommonAssetChecks(global, handle1, 1, asset1);
        handle1.Dispose();
        Assert.That(asset1.WasDisposed, Is.True);

        var (handle2, asset2) = await CommonLoadAsset1(global, prio2, ct);
        CommonAssetChecks(global, handle2, 1, asset2);
        handle2.Dispose();
        Assert.That(asset2.WasDisposed, Is.True);

        Assert.That(asset1, Is.Not.SameAs(asset2));
    }

    private Task<(AssetHandle<GlobalTestAsset>, GlobalTestAsset)> CommonLoadAsset1(
        AssetRegistry global,
        AssetPriority prio,
        CancellationToken ct) =>
        CommonLoadAsset(global, GetInfo(1), prio, ct);

    private async Task<(AssetHandle<GlobalTestAsset>, GlobalTestAsset)> CommonLoadAsset(
        AssetRegistry global,
        TestInfo info,
        AssetPriority prio,
        CancellationToken ct)
    {
        if (prio is AssetPriority.Synchronous)
            info.FinishLoad.TrySetResult();
        var handle = global.Load<TestInfo, GlobalTestAsset>(info, prio);

        if (prio is AssetPriority.Synchronous)
            return (handle, handle.Get());
        if (prio is AssetPriority.Low)
            global.Update();

        info.FinishLoad.TrySetResult();
        return (handle, await Task.Run(() => handle.GetAsync(ct).AsTask(), ct));
    }

    [Test]
    public void DisposeRegistry_SyncAssset()
    {
        var global = new AssetRegistry(DI);
        var handle1 = global.Load<TestInfo, GlobalTestAsset>(GetInfo(1).AsCompleted(), AssetPriority.Synchronous);
        var asset = handle1.Get();
        global.Dispose();

        Assert.That(asset.WasDisposed, Is.True);
        Assert.That(global.WasDisposed, Is.True);
        Assert.That(handle1.Get, Throws.InstanceOf<ObjectDisposedException>());
    }

    [Test]
    public async Task DisposeRegistry_HighAsset_DuringLoad(CancellationToken ct)
    {
        var global = new AssetRegistry(DI);
        var info = GetInfo(1);
        var handle = global.Load<TestInfo, GlobalTestAsset>(info, AssetPriority.High);

        await info.StartedLoad.Task.WaitAsync(ct);
        global.Dispose();
        info.FinishLoad.SetResult();

        // we cannot guarantee that any actual dispose can be called if no asset 
        // reference was ever given to AssetRegistry.
        // In cases of cancellation during asset load, the asset load has to make
        // sure that disposal of objects is called.

        //Assert.That(info.Disposed.Task.IsCompletedSuccessfully, Is.True); 
        Assert.That(handle.Get, Throws.InstanceOf<ObjectDisposedException>());
    }

    [Test]
    public async Task DisposeRegistry_LowAsset_BeforeLoad()
    {
        var global = new AssetRegistry(DI);
        var info = GetInfo(1);
        var handle = global.Load<TestInfo, GlobalTestAsset>(info, AssetPriority.Low);

        global.Dispose();
        Assert.That(info.StartedLoad.Task.IsCompleted, Is.False);
        Assert.That(handle.Get, Throws.InstanceOf<ObjectDisposedException>());
    }

    [Test]
    public void DisposeAsset_AccessAfter()
    {
        using var global = new AssetRegistry(DI);
        var handle = global.Load<TestInfo, GlobalTestAsset>(GetInfo(1).AsCompleted(), AssetPriority.Synchronous);
        handle.Dispose();

        Assert.That(handle.Get, Throws.InstanceOf<ObjectDisposedException>());
    }

    [Test]
    public async Task DisposeAsset_MultiRefs(
        [Values] AssetPriority prio1,
        [Values] AssetPriority prio2,
        [Values] AssetPriority prio3,
        CancellationToken ct)
    {
        using var global = new AssetRegistry(DI);
        var (handle1, asset1) = await CommonLoadAsset1(global, prio1, ct);
        var (handle2, asset2) = await CommonLoadAsset1(global, prio2, ct);
        var (handle3, asset3) = await CommonLoadAsset1(global, prio3, ct);
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
            var thandle = global.Load<TestInfo, GlobalMTDTestAsset>(GetInfo(1).AsCompleted(), AssetPriority.Synchronous);
            handle = thandle;
            asset = thandle.Asset!;
        }
        else
        {
            var thandle = global.Load<TestInfo, GlobalTestAsset>(GetInfo(1).AsCompleted(), AssetPriority.Synchronous);
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

    [Test]
    public async Task DisposeAsset_DuringHighLoad(CancellationToken ct)
    {
        using var global = new AssetRegistry(DI);
        var info = GetInfo(1);

        using var handle1 = global.Load<TestInfo, GlobalTestAsset>(info, AssetPriority.High);
        await info.StartedLoad.Task.WaitAsync(ct);
        handle1.Dispose();
        info.FinishLoad.SetResult();

        await info.Disposed.Task.WaitAsync(ct);
    }

    [Test]
    public async Task DisposeAsset_DuringLowLoad(CancellationToken ct)
    {
        using var global = new AssetRegistry(DI);
        var info = GetInfo(1);

        using var handle1 = global.Load<TestInfo, GlobalTestAsset>(info, AssetPriority.Low);
        global.Update();
        await info.StartedLoad.Task.WaitAsync(ct);
        handle1.Dispose();
        info.FinishLoad.SetResult();

        await info.Disposed.Task.WaitAsync(ct);
    }

    private sealed class TestException : Exception
    {

    }
    private static InstanceOfTypeConstraint ThrowsAssetExceptions =>
        Throws.InstanceOf<TestException>();

    [Test]
    public void Error_SingleSync()
    {
        using var global = new AssetRegistry(DI);
        var info = GetInfo(1).AsErroneous();

        Assert.That(() =>
        {
            global.Load<TestInfo, GlobalTestAsset>(info, AssetPriority.Synchronous);
        }, ThrowsAssetExceptions);
    }

    [Test]
    public async Task Error_SingleHighUnobserved(CancellationToken ct)
    {
        using var global = new AssetRegistry(DI);
        var info = GetInfo(1).AsErroneous();
        using var handle = global.Load<TestInfo, GlobalTestAsset>(info, AssetPriority.High);
        await info.StartedLoad.Task.WaitAsync(ct);
        await Task.Delay(10); // ugly but no real way to check...
    }

    [Test]
    public async Task Error_SingleHighGetAsync(CancellationToken ct)
    {
        using var global = new AssetRegistry(DI);
        var info = GetInfo(1).AsErroneous();
        using var handle = global.Load<TestInfo, GlobalTestAsset>(info, AssetPriority.High);

        await Assert.ThatAsync(async () =>
        {
            _ = await handle.GetAsync(ct);
        }, ThrowsAssetExceptions);
        await Assert.ThatAsync(async () =>
        {
            _ = await handle.GetAsync(ct);
        }, ThrowsAssetExceptions);
    }

    [Test]
    public void Error_SingleHighGetSync()
    {
        using var global = new AssetRegistry(DI);
        var info = GetInfo(1).AsErroneous();
        using var handle = global.Load<TestInfo, GlobalTestAsset>(info, AssetPriority.High);

        Assert.That(() =>
        {
            _ = handle.Get();
        }, ThrowsAssetExceptions);
        Assert.That(() =>
        {
            _ = handle.Get();
        }, ThrowsAssetExceptions);
    }

    [Test]
    public async Task Error_ResetAfterDispose(
        [Values(AssetPriority.Synchronous, AssetPriority.High)] AssetPriority priority,
        [Values] bool getAsync,
        CancellationToken ct)
    {
        using var global = new AssetRegistry(DI);
        using (var handle1 = Load(true)) ;

        using var handle2 = Load(false).Value;
        var asset = getAsync
            ? await handle2.GetAsync(ct)
            : handle2.Get();
        CommonAssetChecks(global, handle2, 1, asset);

        AssetHandle<GlobalTestAsset>? Load(bool withException)
        {
            var info = withException
                ? GetInfo(1).AsErroneous()
                : GetInfo(1).AsCompleted();
            if (priority is AssetPriority.Synchronous && withException)
            {
                Assert.That(() => global.Load<TestInfo, GlobalTestAsset>(info, priority),
                    ThrowsAssetExceptions);
                return null;
            }
            else
                return global.Load<TestInfo, GlobalTestAsset>(info, priority);
        }
    }

    [Test]
    public async Task Error_NoResetWithRefs(CancellationToken ct)
    {
        using var global = new AssetRegistry(DI);
        var info = GetInfo(1).AsErroneous();
        var handle1 = global.Load<TestInfo, GlobalTestAsset>(info, AssetPriority.High);
        var handle2 = global.Load<TestInfo, GlobalTestAsset>(info, AssetPriority.High);

        await Assert.ThatAsync(async () => await handle1.GetAsync(ct), ThrowsAssetExceptions);

        Assert.That(() =>
        {
            handle2.Get();
        }, ThrowsAssetExceptions);

        handle1.Dispose();

        Assert.That(() =>
        {
            handle2.Get();
        }, ThrowsAssetExceptions);
    }

    [Test]
    public void Local_LoadLocalFromGlobal()
    {
        using var global = new AssetRegistry(DI);

        Assert.That(() =>
        {
            global.Load<TestInfo, LocalTestAsset>(GetInfo(1).AsCompleted(), AssetPriority.Synchronous);
        }, Throws.Exception);
    }

    [Test]
    public void Local_LoadGlobalFromLocal()
    {
        using var global = new AssetRegistry(DI);
        using var local = new AssetRegistry(DI, global);

        using var handle = local.Load<TestInfo, GlobalTestAsset>(GetInfo(1).AsCompleted(), AssetPriority.Synchronous);
        var asset = handle.Get();
        CommonAssetChecks(global, handle, 1, asset);
    }

    [Test]
    public void Local_LoadLocalFromLocal()
    {
        using var global = new AssetRegistry(DI);
        using var local = new AssetRegistry(DI, global);

        using var handle = local.Load<TestInfo, LocalTestAsset>(GetInfo(1).AsCompleted(), AssetPriority.Synchronous);
        var asset = handle.Get();
        CommonAssetChecks(local, handle, 1, asset);
    }

    [Test]
    public async Task Secondary_LoadHigh([Values] AssetPriority parentPrio, CancellationToken ct)
    {
        using var global = new AssetRegistry(DI);

        AssetHandle<GlobalTestAsset> childHandle = default;
        var childInfo = GetInfo(2).AsCompleted();
        var parentInfo = GetInfo(1, () =>
            [childHandle = global.Load<TestInfo, GlobalTestAsset>(childInfo, AssetPriority.High)]
        ).AsCompleted();

        var (parentHandle, parentAsset) = await CommonLoadAsset(global, parentInfo, parentPrio, ct);
        CommonAssetChecks(global, parentHandle, 1, parentAsset);
        CommonAssetChecks(global, childHandle, 2);
    }

    [Test]
    public async Task Secondary_HighLoadLow(CancellationToken ct)
    {
        using var global = new AssetRegistry(DI);

        AssetHandle<GlobalTestAsset> childHandle = default;
        var childInfo = GetInfo(2).AsCompleted();
        var parentInfo = GetInfo(1, () =>
            [childHandle = global.Load<TestInfo, GlobalTestAsset>(childInfo, AssetPriority.Low)]
        ).AsCompleted();

        
    }
}
