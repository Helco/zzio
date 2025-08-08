using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using DotNext;
using DotNext.Collections.Generic;
using NUnit.Framework;
using NUnit.Framework.Constraints;

namespace zzre.tests;

[TestFixture(TaskContinuationOptions.None)]
[TestFixture(TaskContinuationOptions.RunContinuationsAsynchronously)]
[TestFixture(TaskContinuationOptions.ExecuteSynchronously)]
[CancelAfter(10000), SingleThreaded]
public class TestAssetRegistry
{
    private interface ITestAsset : IAsset<TestInfo>
    {
        public IAssetRegistry Registry { get; init; }
        public TestInfo Info { get; init; }
        public int Id => Info.Id;
    }

    private readonly struct TestInfo(TaskContinuationOptions tcsOptions, int Id) : IEquatable<TestInfo>
    {
        public readonly int Id = Id;
        public readonly TaskCompletionSource StartedLoad = new(tcsOptions);
        public readonly TaskCompletionSource FinishLoad = new(tcsOptions);
        public readonly TaskCompletionSource Disposed = new(tcsOptions);

        public TestInfo(TaskContinuationOptions tcsOptions, int Id, IAssetHandle[] secondaries)
            : this(tcsOptions, Id) { }

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
            return new(new TAsset() { Info = info, Registry = registry });
        }

        public bool Equals(TestInfo other) => Id == other.Id;
        public override bool Equals([NotNullWhen(true)] object? obj) => obj is TestInfo other ? Equals(other) : false;
        public override int GetHashCode() => Id.GetHashCode();
        public override string ToString() => $"Test {Id}";
    }

    private class GlobalTestAsset : ITestAsset
    {
        public static AssetLocality Locality => AssetLocality.Global;
        public IAssetRegistry Registry { get; init; }
        public TestInfo Info { get; init; }

        public static Task<AssetLoadResult<TestInfo>> LoadAsync(IAssetRegistry registry, Guid _, TestInfo info, CancellationToken ct)
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

        public static Task<AssetLoadResult<TestInfo>> LoadAsync(IAssetRegistry registry, Guid _, TestInfo info, CancellationToken ct)
            => TestInfo.LoadAsync<GlobalMTDTestAsset>(registry, info, ct);

        public void Dispose()
        {
            if (!Registry.IsMainThread)
                Info.Disposed.TrySetException(new AssertionException("MTD asset was not disposed on main thread"));
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

        public static Task<AssetLoadResult<TestInfo>> LoadAsync(IAssetRegistry registry, Guid _, TestInfo info, CancellationToken ct)
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

        public static Task<AssetLoadResult<TestInfo>> LoadAsync(IAssetRegistry registry, Guid _, TestInfo info, CancellationToken ct)
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

    private TestInfo GetInfo(int id) =>
        new TestInfo(tcsOptions, id);

    [Test]
    public void EmptyRegistries()
    {
        using var global = new AssetRegistry(DI);
        using var local = new AssetRegistry(DI, global);
        using var local2 = new AssetRegistry(DI, global);

        Assert.That(global.IsLocalRegistry, Is.False);
        Assert.That(local.IsLocalRegistry, Is.True);
        Assert.That(local2.IsLocalRegistry, Is.True);
        Assert.That(global.DIContainer, Is.SameAs(DI));
    }

    [Test]
    public void RegistryWithLogger()
    {
        DI.AddTag<Serilog.ILogger>(Serilog.Core.Logger.None);
        using var global = new AssetRegistry(DI);
        using var local = new AssetRegistry(DI, global, "local registry");
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

    [Test]
    public async Task UpdateOnNonMainThread(CancellationToken ct)
    {
        using var global = new AssetRegistry(DI);
        await Assert.ThatAsync(() => Task.Run(global.Update), Throws.InvalidOperationException);
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
    public async Task LoadLow_DelRefDuringLoad(CancellationToken ct)
    {
        using var global = new AssetRegistry(DI);
        var info = GetInfo(1);

        using var handle = global.Load<TestInfo, GlobalTestAsset>(info, AssetPriority.Low);
        global.Update();
        await info.StartedLoad.Task.WaitAsync(ct);
        handle.Dispose();
        info.FinishLoad.SetResult();

        await Task.Delay(50);
        global.Update();
    }

    [Test]
    public void LoadLow_ThenGetSync(CancellationToken ct)
    {
        using var global = new AssetRegistry(DI);
        var info = GetInfo(1).AsCompleted();

        using var handle = global.Load<TestInfo, GlobalTestAsset>(info, AssetPriority.Low);
        var asset = handle.Get();
        CommonAssetChecks(global, handle, 1, asset);

        // check whether unnecessary low batch will break something
        global.Update();
        CommonAssetChecks(global, handle, 1, asset);
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
    public void DisposeRegistry_AccessAfter()
    {
        var global = new AssetRegistry(DI);
        using var handle = global.Load<TestInfo, GlobalTestAsset>(GetInfo(1).AsCompleted(), AssetPriority.Synchronous);
        global.Dispose();

        Assert.That(handle.Get, Throws.InstanceOf<ObjectDisposedException>());
    }

    [Test]
    public void DisposeRegistry_DisposeTwice()
    {
        var global = new AssetRegistry(DI);
        global.Load<TestInfo, GlobalTestAsset>(GetInfo(1).AsCompleted(), AssetPriority.Synchronous);
        global.Load<TestInfo, GlobalTestAsset>(GetInfo(2).AsCompleted(), AssetPriority.Synchronous);
        global.Load<TestInfo, GlobalTestAsset>(GetInfo(3).AsCompleted(), AssetPriority.Synchronous);

        global.Dispose();
        global.Dispose();
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

    [Test, Repeat(50, StopOnFailure = true)]
    public async Task DisposeAsset_StressBeforeLowLoad(CancellationToken ct)
    {
        using var global = new AssetRegistry(DI);
        var infos = Enumerable.Range(1, 8).Select(i => GetInfo(i).AsCompleted()).ToArray();
        var handles = infos.Select(info =>
            global.Load<TestInfo, GlobalTestAsset>(info, AssetPriority.Low))
            .ToArray();
        global.Update();
        foreach (var handle in handles)
            handle.Dispose();
        await UpdateAndCheckDisposal(global, infos, ct);
    }

    [Test, Repeat(50, StopOnFailure = true)]
    public async Task DisposeAsset_StressBeforeHighLoad(CancellationToken ct)
    {
        Console.WriteLine("started run");
        using var global = new AssetRegistry(DI);
        await Task.WhenAll(
            Task.Run(() => SingularStress(1), ct),
            Task.Run(() => SingularStress(2), ct),
            Task.Run(() => SingularStress(3), ct),
            Task.Run(() => SingularStress(4), ct),
            Task.Run(() => SingularStress(5), ct),
            Task.Run(() => SingularStress(6), ct),
            Task.Run(() => SingularStress(7), ct),
            Task.Run(() => SingularStress(8), ct)
        ).WaitAsync(ct);
        Console.WriteLine("ended run");

        async Task SingularStress(int id)
        {
            var info = GetInfo(id).AsCompleted();
            var handle = global.Load<TestInfo, GlobalTestAsset>(info, AssetPriority.High);
            handle.Dispose();
            await Task.Yield();
            if (info.StartedLoad.Task.IsCompleted)
                await info.Disposed.Task.WaitAsync(ct);
            // if the load has not started, the disposal will obviously also never happen
        }
    }

    [Test]
    public async Task DisposeAsset_BeforeHighLoad(CancellationToken ct)
    {
        using var global = new AssetRegistry(DI);
        var info = GetInfo(1).AsCompleted();
        var handle = global.Load<TestInfo, GlobalTestAsset>(info, AssetPriority.High);
        handle.Dispose();
        await Task.Delay(50);
        if (info.StartedLoad.Task.IsCompleted)
            await info.Disposed.Task.WaitAsync(ct);
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

    [Test]
    public async Task DisposeAsset_MTDAlreadyDead(CancellationToken ct)
    {
        using var global = new AssetRegistry(DI);
        var info = GetInfo(1);

        using var handle = global.Load<TestInfo, GlobalMTDTestAsset>(info, AssetPriority.High);
        await info.StartedLoad.Task.WaitAsync(ct);
        handle.Dispose();
        info.FinishLoad.SetResult();

        await UpdateAndCheckDisposal(global, [info], ct);
    }

    private async Task UpdateAndCheckDisposal(IAssetRegistry registry, TestInfo[] allInfos, CancellationToken ct)
    {
        var infos = allInfos.ToHashSet();
        while (!ct.IsCancellationRequested && infos.Any())
        {
            await Task.Yield();
            registry.Update();
            infos.RemoveWhere(i =>
            {
                if (!i.StartedLoad.Task.IsCompleted)
                    return true;
                if (!i.Disposed.Task.IsCompleted)
                    return false;
                if (i.Disposed.Task.Exception is Exception e)
                    ExceptionDispatchInfo.Throw(e);
                return true;
            });
        }
        ct.ThrowIfCancellationRequested();
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
    public void Error_LoadLowThenGetSync()
    {
        // this triggers an otherwise uncovered line
        using var global = new AssetRegistry(DI);
        var info = GetInfo(1).AsErroneous();
        using var handle = global.Load<TestInfo, GlobalTestAsset>(info, AssetPriority.Low);

        Assert.That(() => handle.Get(), ThrowsAssetExceptions);
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
    public void Unique_LoadMultiple()
    {
        using var global = new AssetRegistry(DI);
        using var local = new AssetRegistry(DI, global);

        using var handle1 = local.Load<TestInfo, UniqueTestAsset>(GetInfo(1).AsCompleted(), AssetPriority.Synchronous);
        using var handle2 = local.Load<TestInfo, UniqueTestAsset>(GetInfo(1).AsCompleted(), AssetPriority.Synchronous);
        var asset1 = handle1.Get();
        var asset2 = handle2.Get();

        CommonAssetChecks(local, handle1, 1, asset1);
        CommonAssetChecks(local, handle2, 1, asset2);
        Assert.That(handle1, Is.Not.EqualTo(handle2));
        Assert.That(asset1, Is.Not.SameAs(asset2));

        handle1.Dispose();
        Assert.That(handle2.Get, Throws.Nothing); // not disposed
    }

    [Test]
    public async Task LoadNested_AsyncSecondaryHigh([Values] bool parentLow, CancellationToken ct)
    {
        using var global = new AssetRegistry(DI);

        var parentInfo = GetInfo(1);
        using var parentHandle = global.Load<TestInfo, GlobalTestAsset>(parentInfo,
            parentLow ? AssetPriority.Low : AssetPriority.High);
        if (parentLow)
            global.Update();

        // We could load secondary low as the test setup would run the second .Update call on the main thread
        // However that is not really a productive scenario
        await parentInfo.StartedLoad.Task;
        using var childHandle = global.Load<TestInfo, GlobalTestAsset>(GetInfo(2).AsCompleted(), AssetPriority.High);
        var childAsset = await childHandle.GetAsync(ct);
        parentInfo.FinishLoad.SetResult();
        var parentAsset = await parentHandle.GetAsync(ct);

        CommonAssetChecks(global, parentHandle, 1, parentAsset);
        CommonAssetChecks(global, childHandle, 2, childAsset);
    }

    [Test]
    public async Task LoadNested_SyncSecondaryHigh(CancellationToken ct)
    {
        using var global = new AssetRegistry(DI);

        var parentInfo = GetInfo(1);
        AssetHandle<GlobalTestAsset> childHandle = default;
        var loadChildTask = Task.Run(async () =>
        {
            await parentInfo.StartedLoad.Task;
            childHandle = global.Load<TestInfo, GlobalTestAsset>(GetInfo(2).AsCompleted(), AssetPriority.High);
            await childHandle.GetAsync(ct);
            parentInfo.FinishLoad.SetResult();
        }, ct);

        using var parentHandle = global.Load<TestInfo, GlobalTestAsset>(parentInfo, AssetPriority.Synchronous);
        CommonAssetChecks(global, parentHandle, 1);
        CommonAssetChecks(global, childHandle, 2);

        await loadChildTask.WaitAsync(ct); // just to be sure it *finished*
        // the secondary should have been ready after that synchronous primary load
    }

    [Test]
    public async Task LoadNested_Deep(CancellationToken ct)
    {
        using var global = new AssetRegistry(DI);

        var info1 = GetInfo(1);
        var info2 = GetInfo(2);
        var info3 = GetInfo(3);
        var info4 = GetInfo(4).AsCompleted();

        using var handle1 = global.Load<TestInfo, GlobalTestAsset>(info1, AssetPriority.High);
        await info1.StartedLoad.Task;
        using var handle2 = global.Load<TestInfo, GlobalTestAsset>(info2, AssetPriority.High);
        await info2.StartedLoad.Task;
        using var handle3 = global.Load<TestInfo, GlobalTestAsset>(info3, AssetPriority.High);
        await info3.StartedLoad.Task;
        using var handle4 = global.Load<TestInfo, GlobalTestAsset>(info4, AssetPriority.High);
        var asset4 = await handle4.GetAsync(ct);
        info3.FinishLoad.SetResult();
        var asset3 = await handle3.GetAsync(ct);
        info2.FinishLoad.SetResult();
        var asset2 = await handle2.GetAsync(ct);
        info1.FinishLoad.SetResult();
        var asset1 = await handle1.GetAsync(ct);

        CommonAssetChecks(global, handle1, 1, asset1);
        CommonAssetChecks(global, handle2, 2, asset2);
        CommonAssetChecks(global, handle3, 3, asset3);
        CommonAssetChecks(global, handle4, 4, asset4);
    }

    // We cannot detect recursive loads, so we cannot test for it...

    [Test]
    public async Task LoadNested_SyncDoesNotWork(CancellationToken ct)
    {
        // with actual asynchronous loading (so not in the test env main thread)
        // no synchronous loading can be done as that would be main thread material

        using var global = new AssetRegistry(DI);

        AssetHandle<GlobalTestAsset> childHandle = default;
        var parentInfo = GetInfo(1);

        var loadChildTask = Task.Run(async () =>
        {
            try
            {
                await parentInfo.StartedLoad.Task;
                childHandle = global.Load<TestInfo, GlobalTestAsset>(GetInfo(2).AsCompleted(), AssetPriority.Synchronous);
                parentInfo.FinishLoad.SetResult();
            }
            catch (Exception e)
            {
                parentInfo.FinishLoad.SetException(e);
            }
        }, ct);

        Assert.That(() => global.Load<TestInfo, GlobalTestAsset>(parentInfo, AssetPriority.Synchronous),
            Throws.InvalidOperationException);
        // the exception would have been within the parent load so no disposal can be done by AssetRegistry
    }

    [Test]
    public void Handle_Duplicate()
    {
        using var global = new AssetRegistry(DI);

        var info = GetInfo(1).AsCompleted();
        var handle1 = global.Load<TestInfo, GlobalTestAsset>(info, AssetPriority.Synchronous);
        CommonAssetChecks(global, handle1, 1);

        var handle2 = handle1.Duplicate();
        CommonAssetChecks(global, handle2, 1);
        Assert.That(handle2, Is.EqualTo(handle2));

        handle1.Dispose();
        Assert.That(info.Disposed.Task.IsCompleted, Is.False);

        handle2.Dispose();
        Assert.That(info.Disposed.Task.IsCompletedSuccessfully, Is.True);
    }

    [Test]
    public void Handle_Move()
    {
        using var global = new AssetRegistry(DI);

        var info = GetInfo(1).AsCompleted();
        var handle1 = global.Load<TestInfo, GlobalTestAsset>(info, AssetPriority.Synchronous);
        CommonAssetChecks(global, handle1, 1);

        var handle2 = handle1.Move();
        CommonAssetChecks(global, handle2, 1);

        Assert.That(() => handle1.Asset, Throws.Exception);
        Assert.That(() => handle1.Get(), Throws.Exception);
        Assert.That(handle1, Is.EqualTo(handle2)); // equality does not change, Move=Duplicate+Dispose (conceptually)

        handle1.Dispose();
        Assert.That(info.Disposed.Task.IsCompleted, Is.False);

        handle2.Dispose();
        Assert.That(info.Disposed.Task.IsCompletedSuccessfully, Is.True);
    }

    [Test]
    public void Handle_Equality()
    {
        using var global = new AssetRegistry(DI);

        var info1 = GetInfo(1).AsCompleted();
        var info2 = GetInfo(2).AsCompleted();
        var handle1a = global.Load<TestInfo, GlobalTestAsset>(info1, AssetPriority.Synchronous);
        var handle2 = global.Load<TestInfo, GlobalTestAsset>(info2, AssetPriority.Synchronous);
        var handle1b = global.Load<TestInfo, GlobalTestAsset>(info1, AssetPriority.Synchronous);

        Assert.That(handle1a, Is.EqualTo(handle1b));
        Assert.That(handle1a, Is.Not.EqualTo(handle2));
        Assert.That(handle2, Is.Not.EqualTo(handle1b));
        Assert.That(handle1a.GetHashCode(), Is.EqualTo(handle1b.GetHashCode()));

        Assert.That(handle1a == handle1b); // these are just for coverage
        Assert.That(handle2 != handle1a);
        Assert.That(handle1a.Equals((object)handle1b));
        Assert.That(!handle1b.Equals(handle2));
    }

    [Test]
    public void Handle_InvalidCopy_Duplicate()
    {
        using var global = new AssetRegistry(DI);

        var original = global.Load<TestInfo, GlobalTestAsset>(GetInfo(1).AsCompleted(), AssetPriority.Synchronous);
        var invalidCopy = original;
        original.Dispose();

        Assert.That(() => invalidCopy.Duplicate(), Throws.InstanceOf<ObjectDisposedException>());
    }

    [Test]
    public void Handle_InvalidCopy_Access()
    {
        using var global = new AssetRegistry(DI);

        var original = global.Load<TestInfo, GlobalTestAsset>(GetInfo(1).AsCompleted(), AssetPriority.Synchronous);
        var invalidCopy = original;
        original.Dispose();

        Assert.That(() => invalidCopy.Get(), Throws.InstanceOf<ObjectDisposedException>());
    }

    [Test]
    public void Handle_Default_Dispose()
    {
        AssetHandle<GlobalTestAsset> handle = default;
        Assert.That(() =>
        {
            handle.Dispose();
            handle.Dispose();
        }, Throws.Nothing);
    }

    [Test]
    public void GenericHandle_FromAs()
    {
        using var global = new AssetRegistry(DI);
        var thandle = global.Load<TestInfo, GlobalTestAsset>(GetInfo(1).AsCompleted(), AssetPriority.Synchronous);
        var ghandle = thandle.As();

        Assert.That(ghandle.Registry, Is.SameAs(thandle.Registry));
        Assert.That(ghandle.AssetId, Is.EqualTo(thandle.AssetId));
        thandle.Dispose();

        var thandle2 = ghandle.As<GlobalTestAsset>();
        CommonAssetChecks(global, thandle2, 1);
    }

    [Test]
    public void GenericHandle_FromAsAndDisposal()
    {
        using var global = new AssetRegistry(DI);
        var thandle = global.Load<TestInfo, GlobalTestAsset>(GetInfo(1).AsCompleted(), AssetPriority.Synchronous);
        var ghandle = thandle.As();

        Assert.That(ghandle.Registry, Is.SameAs(thandle.Registry));
        Assert.That(ghandle.AssetId, Is.EqualTo(thandle.AssetId));
        thandle.Dispose();
        ghandle.Dispose();

        Assert.That(() =>
        {
            ghandle.As<GlobalTestAsset>().Get();
        }, Throws.Exception);
    }

    [Test]
    public void GenericHandle_FromAsWasDisposed()
    {
        using var global = new AssetRegistry(DI);
        var thandle = global.Load<TestInfo, GlobalTestAsset>(GetInfo(1).AsCompleted(), AssetPriority.Synchronous);
        thandle.Dispose();
        var ghandle = thandle.As();

        Assert.That(() =>
        {
            ghandle.As<GlobalTestAsset>().Get();
        }, Throws.Exception);
    }

    [Test]
    public void GenericHandle_FromDuplicate()
    {
        using var global = new AssetRegistry(DI);
        var thandle = global.Load<TestInfo, GlobalTestAsset>(GetInfo(1).AsCompleted(), AssetPriority.Synchronous);
        var ghandle = thandle.AsDuplicate();

        Assert.That(ghandle.Registry, Is.SameAs(thandle.Registry));
        Assert.That(ghandle.AssetId, Is.EqualTo(thandle.AssetId));

        var thandle2 = ghandle.As<GlobalTestAsset>();
        CommonAssetChecks(global, thandle2, 1);
        CommonAssetChecks(global, thandle, 1);
    }

    [Test]
    public void GenericHandle_TypeCheck()
    {
#if DEBUG
        using var global = new AssetRegistry(DI);
        var thandle = global.Load<TestInfo, GlobalTestAsset>(GetInfo(1).AsCompleted(), AssetPriority.Synchronous);

        var ghandle = thandle.AsDuplicate();
        Assert.That(() =>
        {
            using var _ = ghandle.As<GlobalTestAsset>();
        }, Throws.Nothing);

        ghandle = thandle.AsDuplicate();
        Assert.That(() =>
        {
            using var _ = ghandle.As<LocalTestAsset>(); // no relation   
        }, Throws.InstanceOf<InvalidCastException>());
#else
        Assert.Ignore("Type checks are only done in debug builds");
        return;
#endif
    }

    [Test]
    public void GenericHandle_FromDuplicateAndDisposal()
    {
        using var global = new AssetRegistry(DI);
        var thandle = global.Load<TestInfo, GlobalTestAsset>(GetInfo(1).AsCompleted(), AssetPriority.Synchronous);
        var ghandle = thandle.AsDuplicate();

        Assert.That(ghandle.Registry, Is.SameAs(thandle.Registry));
        Assert.That(ghandle.AssetId, Is.EqualTo(thandle.AssetId));
        thandle.Dispose();
        ghandle.Dispose();

        Assert.That(() =>
        {
            ghandle.As<GlobalTestAsset>().Get();
        }, Throws.Exception);
    }

    [Test]
    public void GenericHandle_FromDuplicateWasDisposed()
    {
        using var global = new AssetRegistry(DI);
        var thandle = global.Load<TestInfo, GlobalTestAsset>(GetInfo(1).AsCompleted(), AssetPriority.Synchronous);
        thandle.Dispose();

        Assert.That(() =>
        {
            var ghandle = thandle.AsDuplicate();
        }, Throws.Exception);
    }


    [Test]
    public void Apply_SyncAfter()
    {
        using var global = new AssetRegistry(DI);

        using var handle = global.Load<TestInfo, GlobalTestAsset>(GetInfo(1).AsCompleted(), AssetPriority.Synchronous);
        int counter = 0;
        global.Apply(handle, _ => counter++);

        Assert.That(counter, Is.EqualTo(1));
    }

    [Test]
    public async Task Apply_HighBeforeLoadStart(CancellationToken ct)
    {
        using var global = new AssetRegistry(DI);
        var info = GetInfo(1);
        using var handle = global.Load<TestInfo, GlobalTestAsset>(info, AssetPriority.High);

        int counter = 0;
        global.Apply(handle, _ => counter++);
        Assert.That(counter, Is.Zero);
        global.Update();
        Assert.That(counter, Is.Zero);

        info.FinishLoad.SetResult();
        var asset = await handle.GetAsync(ct);

        Assert.That(counter, Is.Zero); // apply has to be called on main thread - during Update
        global.Update();
        Assert.That(counter, Is.EqualTo(1));
        global.Update();
        Assert.That(counter, Is.EqualTo(1)); // but not twice
    }

    [Test]
    public async Task Apply_HighDuringLoad(CancellationToken ct)
    {
        using var global = new AssetRegistry(DI);
        var info = GetInfo(1);
        using var handle = global.Load<TestInfo, GlobalTestAsset>(info, AssetPriority.High);
        await info.StartedLoad.Task;

        int counter = 0;
        global.Apply(handle, _ => counter++);
        Assert.That(counter, Is.Zero);
        global.Update();
        Assert.That(counter, Is.Zero);

        info.FinishLoad.SetResult();
        var asset = await handle.GetAsync(ct);

        Assert.That(counter, Is.Zero); // apply has to be called on main thread - during Update
        global.Update();
        Assert.That(counter, Is.EqualTo(1));
        global.Update();
        Assert.That(counter, Is.EqualTo(1)); // but not twice
    }

    [Test]
    public async Task Apply_HighAfterLoad(CancellationToken ct)
    {
        using var global = new AssetRegistry(DI);
        var info = GetInfo(1).AsCompleted();
        using var handle = global.Load<TestInfo, GlobalTestAsset>(info, AssetPriority.High);
        var asset = await handle.GetAsync(ct);

        int counter = 0;
        global.Apply(handle, _ => counter++);
        Assert.That(counter, Is.EqualTo(1)); // main thread and finished loading - fastpath
    }

    [Test]
    public async Task Apply_LowBeforeStart(CancellationToken ct)
    {
        using var global = new AssetRegistry(DI);
        var info = GetInfo(1).AsCompleted();
        using var handle = global.Load<TestInfo, GlobalTestAsset>(info, AssetPriority.Low);

        int counter = 0;
        global.Apply(handle, _ => counter++);
        Assert.That(counter, Is.Zero); // not even started

        global.Update();
        Assert.That(counter, Is.Zero); // just started, not applied

        var asset = await handle.GetAsync(ct);
        Assert.That(counter, Is.Zero); // finished but not applied

        global.Update();
        Assert.That(counter, Is.EqualTo(1));
    }

    [Test]
    public async Task Apply_HighMultipleDuringLoad(CancellationToken ct)
    {
        using var global = new AssetRegistry(DI);
        var info = GetInfo(1);
        using var handle = global.Load<TestInfo, GlobalTestAsset>(info, AssetPriority.High);

        await info.StartedLoad.Task;
        List<int> events = [];
        global.Apply(handle, _ => events.Add(1));
        global.Apply(handle, _ => events.Add(2));
        global.Apply(handle, _ => events.Add(3));
        info.FinishLoad.SetResult();
        var asset = await handle.GetAsync(ct);

        global.Update();
        Assert.That(events, Is.EqualTo([1, 2, 3]));
    }

    [Test]
    public async Task Apply_HighDuringLoadFromAsync(CancellationToken ct)
    {
        using var global = new AssetRegistry(DI);
        var info = GetInfo(1);
        using var handle = global.Load<TestInfo, GlobalTestAsset>(info, AssetPriority.High);

        await info.StartedLoad.Task;
        int counter = 0;
        await Task.Run(() => global.Apply(handle, _ => counter++), ct);
        info.FinishLoad.SetResult();
        await handle.GetAsync(ct);

        Assert.That(counter, Is.Zero); // was not finished 
        global.Update();
        Assert.That(counter, Is.EqualTo(1));
    }

    [Test]
    public async Task Apply_HighAfterLoadFromAsync(CancellationToken ct)
    {
        using var global = new AssetRegistry(DI);
        using var handle = global.Load<TestInfo, GlobalTestAsset>(GetInfo(1).AsCompleted(), AssetPriority.High);
        await handle.GetAsync(ct);

        int counter = 0;
        await Task.Run(() => global.Apply(handle, _ => counter++), ct);

        Assert.That(counter, Is.Zero); // was finished but Apply was not on main thread
        global.Update();
        Assert.That(counter, Is.EqualTo(1));
    }

    [Test]
    public async Task Apply_MixedAsyncOrder(CancellationToken ct)
    {
        using var global = new AssetRegistry(DI);
        using var handle = global.Load<TestInfo, GlobalTestAsset>(GetInfo(1).AsCompleted(), AssetPriority.Low);

        List<int> events = [];
        global.Apply(handle, _ => events.Add(1));
        await Task.Run(() => global.Apply(handle, _ => events.Add(2)), ct);
        global.Apply(handle, _ => events.Add(3));
        await Task.Run(() => global.Apply(handle, _ => events.Add(4)), ct);

        global.Update();
        await handle.GetAsync(ct);
        global.Update();
        Assert.That(events, Is.EqualTo([1, 2, 3, 4]));
    }

    [Test]
    public void Apply_GlobalFromLocal()
    {
        using var global = new AssetRegistry(DI);
        using var local = new AssetRegistry(DI, global);

        using var handle = local.Load<TestInfo, GlobalTestAsset>(GetInfo(1).AsCompleted(), AssetPriority.Synchronous);
        int counter = 0;
        local.Apply(handle, _ => counter++);
        Assert.That(counter, Is.EqualTo(1));
    }

    [Test]
    public void Apply_DefaultHandle()
    {
        using var global = new AssetRegistry(DI);

        Assert.That(() =>
        {
            global.Apply<GlobalTestAsset>(default, _ => { });
        }, Throws.ArgumentException);
    }

    [Test]
    public void Apply_InvalidHandle()
    {
        using var global = new AssetRegistry(DI);

        var handle = global.Load<TestInfo, GlobalTestAsset>(GetInfo(1).AsCompleted(), AssetPriority.Synchronous);
        var invalidCopy = handle;
        handle.Dispose();

        Assert.That(() =>
        {
            global.Apply(invalidCopy, _ => { });
        }, Throws.InstanceOf<ObjectDisposedException>());
    }

    [Test]
    public void Apply_AfterRegistryDisposal()
    {
        var global = new AssetRegistry(DI);
        var handle = global.Load<TestInfo, GlobalTestAsset>(GetInfo(1).AsCompleted(), AssetPriority.Synchronous);
        global.Dispose();

        Assert.That(() =>
        {
            global.Apply(handle, _ => { });
        }, Throws.InstanceOf<ObjectDisposedException>());
    }

    [Test]
    public async Task Apply_AfterAssetDisposal(CancellationToken ct)
    {
        using var global = new AssetRegistry(DI);
        var handle = global.Load<TestInfo, GlobalTestAsset>(GetInfo(1).AsCompleted(), AssetPriority.Low);

        int counter = 0;
        global.Apply(handle, _ => counter++);
        global.Update();
        await handle.GetAsync(ct);
        handle.Dispose(); // now the queue contains one dead asset ID
        global.Update();

        Assert.That(counter, Is.Zero);
    }

    [Test]
    public async Task Apply_AfterAssetRevival(CancellationToken ct)
    {
        using var global = new AssetRegistry(DI);
        var info = GetInfo(1).AsCompleted();
        var handle = global.Load<TestInfo, GlobalTestAsset>(info, AssetPriority.Low);

        int counter = 0;
        global.Apply(handle, _ => counter++);
        global.Update();
        await handle.GetAsync(ct);
        handle.Dispose(); // now the queue contains one dead asset ID
        Assert.That(info.Disposed.Task.IsCompletedSuccessfully); // and it really *is* dead
        Assert.That(counter, Is.Zero);

        handle = global.Load<TestInfo, GlobalTestAsset>(GetInfo(1).AsCompleted(), AssetPriority.Synchronous);
        // now the queue contains the asset ID but not the asset the apply action was targeted at

        global.Update();

        Assert.That(counter, Is.Zero);
    }

    [Test]
    public void Apply_ErrorSync()
    {
        using var global = new AssetRegistry(DI);
        using var handle = global.Load<TestInfo, GlobalTestAsset>(GetInfo(1).AsCompleted(), AssetPriority.Synchronous);

        Assert.That(() =>
        {
            global.Apply(handle, _ => throw new TestException());
        }, Throws.InstanceOf<TestException>());

        _ = handle.Get(); // does not throw because Asset is still valid
    }

    [Test]
    public async Task Apply_ErrorAsync(CancellationToken ct)
    {
        using var global = new AssetRegistry(DI);
        var info1 = GetInfo(1);
        var info2 = GetInfo(2);
        var info3 = GetInfo(3);
        using var handle1 = global.Load<TestInfo, GlobalTestAsset>(info1, AssetPriority.High);
        using var handle2 = global.Load<TestInfo, GlobalTestAsset>(info2, AssetPriority.High);
        using var handle3 = global.Load<TestInfo, GlobalTestAsset>(info3, AssetPriority.High);

        int counter = 0;
        global.Apply(handle1, _ => throw new TestException());
        global.Apply(handle2, _ => counter++);
        global.Apply(handle3, _ => throw new TestException());
        global.Apply(handle1, _ => counter++); // subsequent apply actions are still executed

        info1.FinishLoad.SetResult();
        info2.FinishLoad.SetResult();
        info3.FinishLoad.SetResult();
        await Task.WhenAll([
            handle1.GetAsync(ct).AsTask(),
            handle2.GetAsync(ct).AsTask(),
            handle3.GetAsync(ct).AsTask(),
        ]);

        Assert.That(() =>
        {
            global.Update();
        }, Throws.InstanceOf<AggregateException>());
        Assert.That(counter, Is.EqualTo(2));
    }

    [Test]
    public async Task Apply_ErrorDuringLoad(CancellationToken ct)
    {
        using var global = new AssetRegistry(DI);
        var info = GetInfo(1);
        using var handle = global.Load<TestInfo, GlobalTestAsset>(info, AssetPriority.High);

        int counter = 0;
        global.Apply(handle, _ => counter++);
        info.FinishLoad.SetException(new TestException());

        await Assert.ThatAsync(async () =>
        {
            await handle.GetAsync(ct);
        }, Throws.InstanceOf<TestException>());

        Assert.That(counter, Is.Zero);
        global.Update();
        Assert.That(counter, Is.Zero); // even after Update no apply action of erroneous asset is called
    }

    [Test]
    public async Task Apply_ErrorAfterLoad(CancellationToken ct)
    {
        using var global = new AssetRegistry(DI);
        var info = GetInfo(1).AsErroneous();
        using var handle = global.Load<TestInfo, GlobalTestAsset>(info, AssetPriority.High);

        await Assert.ThatAsync(async () =>
        {
            await handle.GetAsync(ct);
        }, Throws.InstanceOf<TestException>());

        int counter = 0;
        global.Apply(handle, _ => counter++);

        Assert.That(counter, Is.Zero);
        global.Update();
        Assert.That(counter, Is.Zero); // even after Update no apply action of erroneous asset is called
    }

    [Test]
    public void TryGet_AfterLoad()
    {
        using var global = new AssetRegistry(DI);
        using var handle = global.Load<TestInfo, GlobalTestAsset>(GetInfo(1).AsCompleted(), AssetPriority.Synchronous);

        Assert.That(global.TryGet<GlobalTestAsset>(handle.AssetId, out var handle2), Is.True);
        Assert.That(handle2.AssetId, Is.EqualTo(handle.AssetId));
        Assert.That(handle2.Get(), Is.SameAs(handle.Get()));

        handle.Dispose();
        Assert.That(handle2.Get, Throws.Nothing);
        Assert.That(handle2.Dispose, Throws.Nothing);
    }

    [Test]
    public async Task TryGet_DuringLoad(CancellationToken ct)
    {
        using var global = new AssetRegistry(DI);
        var info = GetInfo(1);
        using var handle = global.Load<TestInfo, GlobalTestAsset>(info, AssetPriority.High);

        await info.StartedLoad.Task;
        Assert.That(global.TryGet<GlobalTestAsset>(handle.AssetId, out var handle2), Is.True);
        info.FinishLoad.SetResult();
        var asset = await handle2.GetAsync(ct);
        Assert.That(asset, Is.SameAs(handle.Asset));
        Assert.That(asset, Is.Not.Null);
    }

    [Test]
    public async Task TryGet_BeforeLoad(CancellationToken ct)
    {
        using var global = new AssetRegistry(DI);
        var info = GetInfo(1).AsCompleted();
        using var handle = global.Load<TestInfo, GlobalTestAsset>(info, AssetPriority.Low);

        Assert.That(global.TryGet<GlobalTestAsset>(handle.AssetId, out var handle2), Is.True);
        global.Update();
        var asset = await handle2.GetAsync(ct);
        Assert.That(asset, Is.SameAs(handle.Asset));
        Assert.That(asset, Is.Not.Null);
    }

    [Test]
    public async Task TryGet_ErrorDuringLoad(CancellationToken ct)
    {
        using var global = new AssetRegistry(DI);
        var info = GetInfo(1).AsErroneous();
        using var handle = global.Load<TestInfo, GlobalTestAsset>(info, AssetPriority.High);

        await info.StartedLoad.Task;
        Assert.That(global.TryGet<GlobalTestAsset>(handle.AssetId, out var handle2), Is.True);
        await Assert.ThatAsync(() => handle2.GetAsync(ct).AsTask(), Throws.InstanceOf<TestException>());
    }

    [Test]
    public void TryGet_DefaultId()
    {
        using var global = new AssetRegistry(DI);
        Assert.That(global.TryGet<GlobalTestAsset>(default, out var handle), Is.False);
        Assert.That(handle, Is.Default);
    }

    [Test]
    public void TryGet_InvalidId()
    {
        using var global = new AssetRegistry(DI);
        Assert.That(global.TryGet<GlobalTestAsset>(Guid.NewGuid(), out var handle), Is.False);
        Assert.That(handle, Is.Default);
    }

    [Test]
    public void TryGet_WrongType()
    {
        using var global = new AssetRegistry(DI);
        using var handle = global.Load<TestInfo, GlobalTestAsset>(GetInfo(1).AsCompleted(), AssetPriority.Synchronous);
        Assert.That(global.TryGet<GlobalMTDTestAsset>(handle.AssetId, out var handle2), Is.False);
        Assert.That(handle2, Is.Default);
    }

    [Test]
    public void TryGet_DeadAsset()
    {
        using var global = new AssetRegistry(DI);
        var handle = global.Load<TestInfo, GlobalTestAsset>(GetInfo(1).AsCompleted(), AssetPriority.Synchronous);
        handle.Dispose();
        Assert.That(global.TryGet<GlobalTestAsset>(handle.AssetId, out _), Is.False);
    }

    [Test]
    public void TryGet_LocalFromLocal()
    {
        using var global = new AssetRegistry(DI);
        using var local = new AssetRegistry(DI, global);
        using var handle = local.Load<TestInfo, LocalTestAsset>(GetInfo(1).AsCompleted(), AssetPriority.Synchronous);
        Assert.That(local.TryGet<LocalTestAsset>(handle.AssetId, out _), Is.True);
    }

    [Test]
    public void TryGet_GlobalFromLocal()
    {
        using var global = new AssetRegistry(DI);
        using var local = new AssetRegistry(DI, global);
        using var handle = local.Load<TestInfo, GlobalTestAsset>(GetInfo(1).AsCompleted(), AssetPriority.Synchronous);
        Assert.That(local.TryGet<GlobalTestAsset>(handle.AssetId, out _), Is.True);
    }

    [Test]
    public void TryGet_LocalFromGlobal()
    {
        using var global = new AssetRegistry(DI);
        using var local = new AssetRegistry(DI, global);
        using var handle = local.Load<TestInfo, LocalTestAsset>(GetInfo(1).AsCompleted(), AssetPriority.Synchronous);
        Assert.That(global.TryGet<LocalTestAsset>(handle.AssetId, out _), Is.False);
    }

    [Test]
    public void Stats()
    {
        // Stats are not terribly important, especially in error cases I accept that stats will not be correct
        // this one test should suffice for now
        using var global = new AssetRegistry(DI);
        using var local = new AssetRegistry(DI, global);

        // one asset being loaded twice
        using var h1 = global.Load<TestInfo, GlobalTestAsset>(GetInfo(1).AsCompleted(), AssetPriority.Synchronous);
        using var h11 = global.Load<TestInfo, GlobalTestAsset>(GetInfo(1).AsCompleted(), AssetPriority.Synchronous);

        // one asset being removed
        var h2 = global.Load<TestInfo, GlobalTestAsset>(GetInfo(2).AsCompleted(), AssetPriority.Synchronous);
        h2.Dispose();

        // one asset being created twice
        var h3 = global.Load<TestInfo, GlobalTestAsset>(GetInfo(3).AsCompleted(), AssetPriority.Synchronous);
        h3.Dispose();
        h3 = global.Load<TestInfo, GlobalTestAsset>(GetInfo(3).AsCompleted(), AssetPriority.Synchronous);
        h3.Dispose();

        // two assets being created but not loaded
        var h4 = global.Load<TestInfo, GlobalTestAsset>(GetInfo(4), AssetPriority.High);
        var h5 = global.Load<TestInfo, GlobalTestAsset>(GetInfo(5), AssetPriority.Low);

        // additional assets in the local registry
        using var h6 = local.Load<TestInfo, LocalTestAsset>(GetInfo(6).AsCompleted(), AssetPriority.Synchronous);
        var h7 = local.Load<TestInfo, LocalTestAsset>(GetInfo(7).AsCompleted(), AssetPriority.Synchronous);
        h7.Dispose();

        Assert.That(global.Stats, Is.EqualTo(new AssetRegistryStats(
            created: 6,
            loaded: 4,
            removed: 3,
            total: 3
        )));

        Assert.That(local.Stats, Is.EqualTo(new AssetRegistryStats(
            created: 6 + 2,
            loaded: 4 + 2,
            removed: 3 + 1,
            total: 3 + 1
        )));

        // just to fill code coverage
        Assert.That(() =>
        {
            var stats = global.Stats;
            var a = stats - stats;
            _ = stats.Created;
            _ = stats.Loaded;
            _ = stats.Removed;
            _ = stats.Total;
            _ = stats.ToString();
        }, Throws.Nothing);
    }

    [Test]
    public void Delayed_Undelayed()
    {
        using var global = new AssetRegistryDelayed(new AssetRegistry(DI));

        var handle1 = global.Load<TestInfo, GlobalTestAsset>(GetInfo(1).AsCompleted(), AssetPriority.Synchronous);
        var handle2 = handle1; // we use an invalid copy to check for asset disposal
        handle1.Dispose();

        Assert.That(handle2.Get, Throws.InstanceOf<ObjectDisposedException>());
    }

    [Test]
    public void Delayed_Delayed()
    {
        using var global = new AssetRegistryDelayed(new AssetRegistry(DI));
        global.DelayDisposals = true;

        var handle1 = global.Load<TestInfo, GlobalTestAsset>(GetInfo(1).AsCompleted(), AssetPriority.Synchronous);
        var handle2 = handle1; // we use an invalid copy to check for asset disposal
        handle1.Dispose();

        Assert.That(handle2.Get, Throws.Nothing);

        global.DelayDisposals = false;
        Assert.That(handle2.Get, Throws.Nothing);
    }

    [Test]
    public void Delayed_DelayedTwice()
    {
        using var global = new AssetRegistryDelayed(new AssetRegistry(DI));
        global.DelayDisposals = true;

        var handle1 = global.Load<TestInfo, GlobalTestAsset>(GetInfo(1).AsCompleted(), AssetPriority.Synchronous);
        var handle2 = global.Load<TestInfo, GlobalTestAsset>(GetInfo(1).AsCompleted(), AssetPriority.Synchronous);
        handle1.Dispose();

        Assert.That(handle2.Get, Throws.Nothing);

        global.DelayDisposals = false;
        Assert.That(handle2.Get, Throws.Nothing);

        global.DelayDisposals = true;
        global.DelayDisposals = false;
        Assert.That(handle2.Get, Throws.Nothing); // still alive
    }
}
