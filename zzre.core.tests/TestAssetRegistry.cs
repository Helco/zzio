using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using NUnit.Framework.Constraints;

namespace zzre.tests;

[TestFixture(TaskContinuationOptions.None)]
[TestFixture(TaskContinuationOptions.RunContinuationsAsynchronously)]
[Apartment(System.Threading.ApartmentState.STA), CancelAfter(1000)]
[Timeout(3000)]
public class TestAssetRegistry
{
    class SynchronousAsset : Asset
    {
        public SynchronousAsset(IAssetRegistry registry, Guid id) : base(registry, id)
        {
        }

        protected override IEnumerable<AssetHandle> Load()
        {
            return NoSecondaryAssets;
        }

        protected override void Unload()
        {
        }
    }

    class SynchronousGlobalAsset : SynchronousAsset
    {
        public SynchronousGlobalAsset(IAssetRegistry registry, Guid id, Info info) : base(registry, id)
        {
            InfoID = info.id;
        }

        public readonly record struct Info(int id);
        public int InfoID { get; }
    }

    class SynchronousContextAsset : SynchronousAsset
    {
        public SynchronousContextAsset(IAssetRegistry registry, Guid id, Info info) : base(registry, id)
        {
            InfoID = info.id;
        }

        public readonly record struct Info(int id);
        public int InfoID { get; }
    }

    class SynchronousSingleUsageAsset : SynchronousAsset
    {
        public SynchronousSingleUsageAsset(IAssetRegistry registry, Guid id, Info info) : base(registry, id)
        {
            InfoID = info.id;
        }

        public readonly record struct Info(int id);
        public int InfoID { get; }
    }

    class ManualGlobalAsset : Asset
    {
        public ManualGlobalAsset(IAssetRegistry registry, Guid id, Info info) : base(registry, id)
        {
            this.info = info;
        }

        public class Info(TaskContinuationOptions tcsOptions, int id, bool waitForSecondary = true) : IEquatable<Info>
        {
            public readonly int ID = id;
            public readonly bool WaitForSecondary = waitForSecondary;
            public readonly TaskCompletionSource<IEnumerable<AssetHandle>> Completion = new();
            public readonly TaskCompletionSource WasStarted = new();
            public readonly TaskCompletionSource WasUnloaded = new();
            
            public void Complete(params AssetHandle[] secondary) => Completion.SetResult(secondary);
            public void Fail() => Completion.SetException(new IOException("Oh no, something failed"));

            bool IEquatable<Info>.Equals(Info? other) => ID == other?.ID;
        }

        public int InfoID => info.ID;
        protected override bool NeedsSecondaryAssets => info.WaitForSecondary;
        private readonly Info info;

        protected override IEnumerable<AssetHandle> Load()
        {
            info.WasStarted.SetResult(); // throws if we enter twice. Good.
            if (info.Completion.Task.IsCompletedSuccessfully)
                return info.Completion.Task.Result;
            else
                return LoadAsynchronously;
        }

        protected override Task<IEnumerable<AssetHandle>> LoadAsync() => info.Completion.Task;

        protected override void Unload()
        {
            info.WasUnloaded.SetResult();
        }
    }

    static TestAssetRegistry()
    {
        AssetInfoRegistry<SynchronousGlobalAsset.Info>.Register<SynchronousGlobalAsset>(AssetLocality.Global);
        AssetInfoRegistry<SynchronousContextAsset.Info>.Register<SynchronousContextAsset>(AssetLocality.Context);
        AssetInfoRegistry<SynchronousSingleUsageAsset.Info>.Register<SynchronousSingleUsageAsset>(AssetLocality.SingleUsage);

        AssetInfoRegistry<ManualGlobalAsset.Info>.Register<ManualGlobalAsset>(AssetLocality.Global);
    }

    private TagContainer diContainer;
    private AssetRegistry globalRegistry;
    private AssetLocalRegistry localRegistry;
    private readonly TaskContinuationOptions tcsOptions;

    public TestAssetRegistry(TaskContinuationOptions tcsOptions) => this.tcsOptions = tcsOptions;

    [SetUp]
    public void Setup()
    {
        diContainer = new();
        diContainer.AddTag<Serilog.ILogger>(Serilog.Core.Logger.None);
        diContainer.AddTag<IAssetRegistry>(globalRegistry = new AssetRegistry("Global", diContainer));
        localRegistry = new AssetLocalRegistry("Local", diContainer);
        TestContext.CurrentContext.CancellationToken.Register(() => Cleanup("Cancellation"));
    }

    [TearDown]
    public void TearDown() => Cleanup("TearDown");

    private void Cleanup(string reason)
    {
        try
        {
            localRegistry.Dispose();
            diContainer.Dispose();
            if (TestContext.CurrentContext.CancellationToken.IsCancellationRequested)
                Assert.Fail("Test was cancelled, most likely due to a timeout.");
        }
        catch(Exception e)
        {
            Assert.Fail($"Exception during {reason}: {e}");
        }
    }

    [Test]
    public void EmptyConstruction() {}

    [Test]
    public void LoadSyncGlobalAsset_GlobalRegistry()
    {
        var assetHandle = globalRegistry.Load(new SynchronousGlobalAsset.Info(42), AssetLoadPriority.Synchronous, null);
        Assert.That(assetHandle.IsLoaded);
        Assert.That(assetHandle.Registry, Is.SameAs(globalRegistry));
        Assert.That(assetHandle.AssetID, Is.Not.Default);

        var asset = assetHandle.Get<SynchronousGlobalAsset>();
        Assert.That(asset.ID, Is.EqualTo(assetHandle.AssetID));
        Assert.That(asset.Registry, Is.SameAs(globalRegistry));
        Assert.That(asset.State, Is.EqualTo(AssetState.Loaded));
        Assert.That(asset.InfoID, Is.EqualTo(42));

        assetHandle.Dispose(); // Because this is a synchronous asset, after this method
        globalRegistry.ApplyAssets(); // the asset should be queued up for deletion already

        Assert.That(asset.State, Is.EqualTo(AssetState.Disposed));
    }

    // TODO: Reconsider AssetHandle.Registry from user-facing perspective

    [Test]
    public void LoadSyncGlobalAsset_LocalRegistry()
    {
        // just check that it works at all
        using var assetHandle = localRegistry.Load(new SynchronousGlobalAsset.Info(42), AssetLoadPriority.Synchronous, null);
        Assert.That(assetHandle.IsLoaded);
        Assert.That(assetHandle.Registry, Is.SameAs(globalRegistry));
        Assert.That(assetHandle.Get<SynchronousGlobalAsset>().InfoID, Is.EqualTo(42));
    }

    [Test]
    public void LoadSyncContextAsset_GlobalRegistry()
    {
        Assert.That(() =>
        {
            globalRegistry.Load(new SynchronousContextAsset.Info(42), AssetLoadPriority.Synchronous, null);
        }, Throws.InvalidOperationException);
    }

    [Test] 
    public void LoadSyncContextAsset_LocalRegistry()
    {
        using var assetHandle = localRegistry.Load(new SynchronousContextAsset.Info(42), AssetLoadPriority.Synchronous, null);
        Assert.That(assetHandle.IsLoaded);
        // Assert.That(assetHandle.Registry, Is.SameAs(localRegistry)); // TODO: Fix this before merge
        Assert.That(assetHandle.Get<SynchronousContextAsset>().InfoID, Is.EqualTo(42));
    }

    [Test]
    public void LoadSyncSingleUsageAsset_GlobalRegistry()
    {
        Assert.That(() =>
        {
            globalRegistry.Load(new SynchronousSingleUsageAsset.Info(42), AssetLoadPriority.Synchronous, null);
        }, Throws.InvalidOperationException);
    }

    [Test]
    public void LoadSyncSingleUsageAsset_LocalRegistry()
    {
        using var assetHandle = localRegistry.Load(new SynchronousSingleUsageAsset.Info(42), AssetLoadPriority.Synchronous, null);
        Assert.That(assetHandle.IsLoaded);
        // Assert.That(assetHandle.Registry, Is.SameAs(localRegistry)); // TODO: Fix this before merge
        Assert.That(assetHandle.Get<SynchronousSingleUsageAsset>().InfoID, Is.EqualTo(42));
    }

    [Test]
    public void LoadSyncGlobalAsset_MultipleLoads()
    {
        using var assetHandleA1 = globalRegistry.Load(new SynchronousGlobalAsset.Info(42), AssetLoadPriority.Synchronous, null);
        using var assetHandleA2 = globalRegistry.Load(new SynchronousGlobalAsset.Info(42), AssetLoadPriority.Synchronous, null);
        using var assetHandleB1 = globalRegistry.Load(new SynchronousGlobalAsset.Info(1337), AssetLoadPriority.Synchronous, null);
        using var assetHandleA3 = globalRegistry.Load(new SynchronousGlobalAsset.Info(42), AssetLoadPriority.Synchronous, null);

        Assert.That(assetHandleA1, Is.EqualTo(assetHandleA2));
        Assert.That(assetHandleA2, Is.EqualTo(assetHandleA3));
        Assert.That(assetHandleA3, Is.Not.EqualTo(assetHandleB1));

        Assert.That(assetHandleA1.AssetID, Is.EqualTo(assetHandleA2.AssetID));
        Assert.That(assetHandleA1.AssetID, Is.Not.EqualTo(assetHandleB1.AssetID));

        var assetA1 = assetHandleA1.Get<SynchronousGlobalAsset>();
        var assetA2 = assetHandleA2.Get<SynchronousGlobalAsset>();
        var assetB1 = assetHandleB1.Get<SynchronousGlobalAsset>();
        Assert.That(assetA1, Is.SameAs(assetA2));
        Assert.That(assetA1, Is.Not.SameAs(assetB1));
    }

    [Test]
    public void LoadSyncContextAsset_MultipleLoads()
    {
        using var assetHandleA1 = localRegistry.Load(new SynchronousContextAsset.Info(42), AssetLoadPriority.Synchronous, null);
        using var assetHandleA2 = localRegistry.Load(new SynchronousContextAsset.Info(42), AssetLoadPriority.Synchronous, null);
        using var assetHandleB1 = localRegistry.Load(new SynchronousContextAsset.Info(1337), AssetLoadPriority.Synchronous, null);
        using var assetHandleA3 = localRegistry.Load(new SynchronousContextAsset.Info(42), AssetLoadPriority.Synchronous, null);

        Assert.That(assetHandleA1, Is.EqualTo(assetHandleA2));
        Assert.That(assetHandleA2, Is.EqualTo(assetHandleA3));
        Assert.That(assetHandleA3, Is.Not.EqualTo(assetHandleB1));

        Assert.That(assetHandleA1.AssetID, Is.EqualTo(assetHandleA2.AssetID));
        Assert.That(assetHandleA1.AssetID, Is.Not.EqualTo(assetHandleB1.AssetID));

        var assetA1 = assetHandleA1.Get<SynchronousContextAsset>();
        var assetA2 = assetHandleA2.Get<SynchronousContextAsset>();
        var assetB1 = assetHandleB1.Get<SynchronousContextAsset>();
        Assert.That(assetA1, Is.SameAs(assetA2));
        Assert.That(assetA1, Is.Not.SameAs(assetB1));
    }

    [Test]
    public void LoadSyncContextAsset_MultipleLocalRegistries()
    {
        using var localRegistry2 = new AssetLocalRegistry("OtherLocal", diContainer);
        using var assetHandle1 = localRegistry.Load(new SynchronousContextAsset.Info(42), AssetLoadPriority.Synchronous, null);
        using var assetHandle2 = localRegistry2.Load(new SynchronousContextAsset.Info(42), AssetLoadPriority.Synchronous, null);

        Assert.That(assetHandle1.AssetID, Is.EqualTo(assetHandle2.AssetID));
        Assert.That(assetHandle1, Is.Not.EqualTo(assetHandle2));

        var asset1 = assetHandle1.Get<SynchronousContextAsset>();
        var asset2 = assetHandle2.Get<SynchronousContextAsset>();
        Assert.That(asset1, Is.Not.SameAs(asset2));
    }

    [Test]
    public void LoadSyncSingleUsageAsset_MultipleLoads()
    {
        using var assetHandle1 = localRegistry.Load(new SynchronousSingleUsageAsset.Info(42), AssetLoadPriority.Synchronous, null);
        using var assetHandle2 = localRegistry.Load(new SynchronousSingleUsageAsset.Info(42), AssetLoadPriority.Synchronous, null);

        Assert.That(assetHandle1, Is.Not.EqualTo(assetHandle2));

        var asset1 = assetHandle1.Get<SynchronousSingleUsageAsset>();
        var asset2 = assetHandle2.Get<SynchronousSingleUsageAsset>();
        Assert.That(asset1, Is.Not.SameAs(asset2));

        assetHandle1.Dispose();
        Assert.That(assetHandle2.IsLoaded);
        Assert.That(asset2.State, Is.EqualTo(AssetState.Loaded));
        Assert.That(asset1.State, Is.EqualTo(AssetState.Disposed));
    }

    [Test]
    public void ApplySyncAsset_Action()
    {
        int callCount = 0;
        using var assetHandle = globalRegistry.Load(new SynchronousGlobalAsset.Info(42), AssetLoadPriority.Synchronous, assetHandle =>
        {
            callCount++;
            Assert.That(assetHandle.IsLoaded);
            Assert.That(assetHandle.Get<SynchronousGlobalAsset>().InfoID, Is.EqualTo(42));
        });

        Assert.That(callCount, Is.EqualTo(1));
    }

    private static void IncrementIntegerSync(AssetHandle assetHandle, in StrongBox<int> callCount)
    {
        Assert.That(assetHandle.IsLoaded);
        Assert.That(assetHandle.Get<SynchronousGlobalAsset>().InfoID, Is.EqualTo(42));
        callCount.Value++;
    }

    [Test]
    public unsafe void ApplySyncAsset_FnPtr()
    {
        StrongBox<int> callCount = new(0);
        using var assetHandle = globalRegistry.Load(new SynchronousGlobalAsset.Info(42), AssetLoadPriority.Synchronous,
            &IncrementIntegerSync, callCount);
        Assert.That(callCount.Value, Is.EqualTo(1));
    }

    [Test]
    public unsafe void ApplySyncAsset_ApplyAfterLoad()
    {
        StrongBox<int> callCountFnPtr = new(0);
        using var assetHandle1 = globalRegistry.Load(new SynchronousGlobalAsset.Info(42), AssetLoadPriority.Synchronous,
            &IncrementIntegerSync, callCountFnPtr);
        Assert.That(callCountFnPtr.Value, Is.EqualTo(1));

        int callCountAction = 0;
        using var assetHandle2 = globalRegistry.Load(new SynchronousGlobalAsset.Info(42), AssetLoadPriority.Synchronous,
            _ => callCountAction++);
        Assert.That(callCountAction, Is.EqualTo(1));

        Assert.That(callCountFnPtr.Value, Is.EqualTo(1)); // checks we have not called the previous apply action twice
    }

    [Test]
    public unsafe void ApplySyncAsset_OverHandle()
    {
        using var assetHandle = globalRegistry.Load(new SynchronousGlobalAsset.Info(42), AssetLoadPriority.Synchronous, null);

        StrongBox<int> callCountFnPtr = new(0);
        assetHandle.Apply(&IncrementIntegerSync, callCountFnPtr);
        Assert.That(callCountFnPtr.Value, Is.EqualTo(1));

        int callCountAction = 0;
        assetHandle.Apply(_ => callCountAction++);
        Assert.That(callCountAction, Is.EqualTo(1));

        Assert.That(callCountFnPtr.Value, Is.EqualTo(1));
    }

    [Test]
    public async Task ApplySyncAsset_AsyncApplyByLoad()
    {
        using var assetHandle = globalRegistry.Load(new SynchronousGlobalAsset.Info(42), AssetLoadPriority.Synchronous, null);

        int callCountAction = 0;
        var mainThreadId = Environment.CurrentManagedThreadId;
        await Task.Factory.StartNew(async () =>
        {
            Assert.That(Environment.CurrentManagedThreadId, Is.Not.EqualTo(mainThreadId));
            using var secondHandle = globalRegistry.Load(new SynchronousGlobalAsset.Info(42), AssetLoadPriority.High, _ => callCountAction++);
            await (globalRegistry as IAssetRegistry).WaitAsyncAll(secondHandle);
        }, TaskCreationOptions.LongRunning); // this option should ensure a new thread

        Assert.That(callCountAction, Is.EqualTo(0)); // otherwise the apply action would have been called on a non-main thread
        globalRegistry.ApplyAssets();
        Assert.That(callCountAction, Is.EqualTo(1));
    }

    [Test]
    public async Task ApplySyncAsset_AsyncApplyByHandle()
    {
        using var assetHandle = globalRegistry.Load(new SynchronousGlobalAsset.Info(42), AssetLoadPriority.Synchronous, null);

        int callCountAction = 0;
        var mainThreadId = Environment.CurrentManagedThreadId;
        await Task.Factory.StartNew(async () =>
        {
            Assert.That(Environment.CurrentManagedThreadId, Is.Not.EqualTo(mainThreadId));
            assetHandle.Apply(_ => callCountAction++);
        }, TaskCreationOptions.LongRunning); // this option should ensure a new thread

        Assert.That(callCountAction, Is.EqualTo(0)); // otherwise the apply action would have been called on a non-main thread
        globalRegistry.ApplyAssets();
        Assert.That(callCountAction, Is.EqualTo(1));
    }

    [Test]
    public void LoadAsyncAsset_HighSync()
    {
        var assetInfo = new ManualGlobalAsset.Info(tcsOptions, 42);
        using var assetHandle = globalRegistry.Load(assetInfo, AssetLoadPriority.High, null);
        Assert.That(assetHandle.IsLoaded, Is.False);
        // we cannot reason about WasStarted, as it is called asynchronously
        Assert.That(assetInfo.WasUnloaded.Task.IsCompleted, Is.False);

        assetInfo.Complete();
        var asset = assetHandle.Get<ManualGlobalAsset>();

        Assert.That(assetHandle.IsLoaded, Is.True);
        Assert.That(assetInfo.WasStarted.Task.IsCompletedSuccessfully, Is.True);
        Assert.That(assetInfo.WasUnloaded.Task.IsCompleted, Is.False);
    }

    [Test]
    public void LoadAsyncAsset_LowSync()
    {
        var assetInfo = new ManualGlobalAsset.Info(tcsOptions, 42);
        using var assetHandle = globalRegistry.Load(assetInfo, AssetLoadPriority.Low, null);
        Assert.That(assetHandle.IsLoaded, Is.False);
        Assert.That(assetInfo.WasStarted.Task.IsCompleted, Is.False);
        Assert.That(assetInfo.WasUnloaded.Task.IsCompleted, Is.False);

        globalRegistry.ApplyAssets();
        assetInfo.Complete();
        var asset = assetHandle.Get<ManualGlobalAsset>();

        Assert.That(assetHandle.IsLoaded, Is.True);
        Assert.That(assetInfo.WasStarted.Task.IsCompletedSuccessfully, Is.True);
        Assert.That(assetInfo.WasUnloaded.Task.IsCompleted, Is.False);
    }

    [Test]
    public async Task LoadAsyncAsset_HighAsync()
    {
        var assetInfo = new ManualGlobalAsset.Info(tcsOptions, 42);
        using var assetHandle = globalRegistry.Load(assetInfo, AssetLoadPriority.High, null);
        Assert.That(assetHandle.IsLoaded, Is.False);
        // we cannot reason about WasStarted, as it is called asynchronously
        Assert.That(assetInfo.WasUnloaded.Task.IsCompleted, Is.False);

        assetInfo.Complete();
        await (globalRegistry as IAssetRegistry).WaitAsyncAll(assetHandle);

        Assert.That(assetHandle.IsLoaded, Is.True);
        Assert.That(assetInfo.WasStarted.Task.IsCompletedSuccessfully, Is.True);
        Assert.That(assetInfo.WasUnloaded.Task.IsCompleted, Is.False);
    }

    [Test]
    public async Task LoadAsyncAsset_LowAsync()
    {
        var assetInfo = new ManualGlobalAsset.Info(tcsOptions, 42);
        using var assetHandle = globalRegistry.Load(assetInfo, AssetLoadPriority.Low, null);
        Assert.That(assetHandle.IsLoaded, Is.False);
        Assert.That(assetInfo.WasStarted.Task.IsCompleted, Is.False);
        Assert.That(assetInfo.WasUnloaded.Task.IsCompleted, Is.False);

        globalRegistry.ApplyAssets();
        assetInfo.Complete();
        await (globalRegistry as IAssetRegistry).WaitAsyncAll(assetHandle);

        Assert.That(assetHandle.IsLoaded, Is.True);
        Assert.That(assetInfo.WasStarted.Task.IsCompletedSuccessfully, Is.True);
        Assert.That(assetInfo.WasUnloaded.Task.IsCompleted, Is.False);
    }

    [Test]
    public async Task LoadAsyncAsset_LowThenHigh()
    {
        var assetInfo = new ManualGlobalAsset.Info(tcsOptions, 42);
        assetInfo.Complete();

        using var assetHandleLow = globalRegistry.Load(assetInfo, AssetLoadPriority.Low, null);
        using var assetHandleHigh = globalRegistry.Load(assetInfo, AssetLoadPriority.High, null);

        await (globalRegistry as IAssetRegistry).WaitAsyncAll(assetHandleHigh);
        Assert.That(assetHandleHigh.IsLoaded, Is.True); // without ever ApplyAssets to start loading as low

        globalRegistry.ApplyAssets();
        await (globalRegistry as IAssetRegistry).WaitAsyncAll(assetHandleLow);

        Assert.That(assetHandleLow.IsLoaded, Is.True); // mainly checking that no exception came during the second load
    }

    [Test]
    public async Task LoadAsyncAsset_LowThenSynchronous()
    {
        var assetInfo = new ManualGlobalAsset.Info(tcsOptions, 42);
        assetInfo.Complete();
        
        using var assetHandleLow = globalRegistry.Load(assetInfo, AssetLoadPriority.Low, null);
        using var assetHandleSync = globalRegistry.Load(assetInfo, AssetLoadPriority.Synchronous, null);

        Assert.That(assetHandleLow.IsLoaded, Is.True);

        globalRegistry.ApplyAssets();
        await (globalRegistry as IAssetRegistry).WaitAsyncAll(assetHandleSync);

        Assert.That(assetHandleSync.IsLoaded, Is.True);
    }

    [Test]
    public async Task UnloadAsyncAsset_HighNormal()
    {
        var assetInfo = new ManualGlobalAsset.Info(tcsOptions, 42);
        using var assetHandle = globalRegistry.Load(assetInfo, AssetLoadPriority.High, null);
        assetInfo.Complete();
        await (globalRegistry as IAssetRegistry).WaitAsyncAll(assetHandle);
        Assert.That(assetHandle.IsLoaded, Is.True);

        assetHandle.Dispose();
        globalRegistry.ApplyAssets();

        Assert.That(assetHandle.IsLoaded, Is.False);
        Assert.That(assetInfo.WasUnloaded.Task.IsCompletedSuccessfully, Is.True);
    }

    [Test]
    public async Task UnloadAsyncAsset_LowNormal()
    {
        var assetInfo = new ManualGlobalAsset.Info(tcsOptions, 42);
        using var assetHandle = globalRegistry.Load(assetInfo, AssetLoadPriority.Low, null);
        assetInfo.Complete();
        globalRegistry.ApplyAssets();
        await (globalRegistry as IAssetRegistry).WaitAsyncAll(assetHandle);
        Assert.That(assetHandle.IsLoaded, Is.True);

        assetHandle.Dispose();
        globalRegistry.ApplyAssets();

        Assert.That(assetHandle.IsLoaded, Is.False);
        Assert.That(assetInfo.WasUnloaded.Task.IsCompletedSuccessfully, Is.True);
    }

    [Test]
    public async Task UnloadAsyncAsset_HighDuringLoad()
    {
        var assetInfo = new ManualGlobalAsset.Info(tcsOptions, 42);
        using var assetHandle = globalRegistry.Load(assetInfo, AssetLoadPriority.High, null);
        await assetInfo.WasStarted.Task;

        assetHandle.Dispose();
        globalRegistry.ApplyAssets();
        assetInfo.Complete();

        Assert.That(assetHandle.IsLoaded, Is.False);
        Assert.That(assetInfo.WasUnloaded.Task.IsCompletedSuccessfully, Is.True);
    }

    [Test]
    public async Task UnloadAsyncAsset_LowDuringLoad()
    {
        var assetInfo = new ManualGlobalAsset.Info(tcsOptions, 42);
        using var assetHandle = globalRegistry.Load(assetInfo, AssetLoadPriority.Low, null);
        globalRegistry.ApplyAssets();
        await assetInfo.WasStarted.Task;

        assetHandle.Dispose();
        globalRegistry.ApplyAssets();
        assetInfo.Complete();

        Assert.That(assetHandle.IsLoaded, Is.False);
        Assert.That(assetInfo.WasUnloaded.Task.IsCompletedSuccessfully, Is.True);
    }

    [Test]
    public async Task UnloadAsyncAsset_HighBeforeLoad()
    {
        var assetInfo = new ManualGlobalAsset.Info(tcsOptions, 42);
        using var assetHandle = globalRegistry.Load(assetInfo, AssetLoadPriority.High, null);
        assetHandle.Dispose();
        if (assetInfo.WasStarted.Task.IsCompletedSuccessfully)
            // yes, this is not fully correct but also not terrible if *this* test not always succeeds
            Assert.Inconclusive("Asset was already started, unsynchronizable test will not be conclusive");

        assetInfo.Complete();
        await Task.Yield();

        Assert.That(assetHandle.IsLoaded, Is.False);
        Assert.That(assetInfo.WasUnloaded.Task.IsCompletedSuccessfully, Is.True);
    }

    [Test]
    public async Task UnloadAsyncAsset_LowBeforeLoad()
    {
        var assetInfo = new ManualGlobalAsset.Info(tcsOptions, 42);
        using var assetHandle = globalRegistry.Load(assetInfo, AssetLoadPriority.Low, null);
        assetHandle.Dispose();
        globalRegistry.ApplyAssets();

        assetInfo.Complete();
        await Task.Yield();

        Assert.That(assetHandle.IsLoaded, Is.False);
        Assert.That(assetInfo.WasUnloaded.Task.IsCompletedSuccessfully, Is.True);
    }

    [Test]
    public Task UnloadAsyncAsset_High_AccessDisposedHandle()
    {
        var assetInfo = new ManualGlobalAsset.Info(tcsOptions, 42);
        using var assetHandle = globalRegistry.Load(assetInfo, AssetLoadPriority.High, null);
        if (assetInfo.WasStarted.Task.IsCompletedSuccessfully)
            Assert.Inconclusive("Asset was already started, unsynchronizable test will not be conclusive");
        assetHandle.Dispose();
        globalRegistry.ApplyAssets();

        assetInfo.Complete();
        return Assert.ThatAsync(
            () => (globalRegistry as IAssetRegistry).WaitAsyncAll(assetHandle),
            Throws.InstanceOf<ObjectDisposedException>());
    }

    [Test]
    public Task UnloadAsyncAsset_Low_AccessDisposedHandle()
    {
        var assetInfo = new ManualGlobalAsset.Info(tcsOptions, 42);
        using var assetHandle = globalRegistry.Load(assetInfo, AssetLoadPriority.Low, null);
        assetHandle.Dispose();
        globalRegistry.ApplyAssets();

        assetInfo.Complete();
        return Assert.ThatAsync(
            () => (globalRegistry as IAssetRegistry).WaitAsyncAll(assetHandle),
            Throws.InstanceOf<ObjectDisposedException>());
    }

    private static void IncrementIntegerAsync(AssetHandle assetHandle, in StrongBox<int> callCount)
    {
        Assert.That(assetHandle.IsLoaded);
        Assert.That(assetHandle.Get<ManualGlobalAsset>().InfoID, Is.EqualTo(42));
        callCount.Value++;
    }

    private unsafe void ApplyIncrementInteger(AssetHandle assetHandle, StrongBox<int> callCount)
    {
        assetHandle.Apply(&IncrementIntegerAsync, callCount);
    }

    [Test]
    public async Task ApplyAsyncAsset_BeforeLoadingStarted()
    {
        int callCountAction = 0;
        StrongBox<int> callCountFnPtr = new(0);
        var assetInfo = new ManualGlobalAsset.Info(tcsOptions, 42);
        using var assetHandle = globalRegistry.Load(assetInfo, AssetLoadPriority.Low, null);

        assetHandle.Apply(_ => callCountAction++);
        ApplyIncrementInteger(assetHandle, callCountFnPtr); // we cannot use unsafe but can call unsafe functions...

        globalRegistry.ApplyAssets();
        assetInfo.Complete();
        await (globalRegistry as IAssetRegistry).WaitAsyncAll(assetHandle);
        globalRegistry.ApplyAssets(); // preapply actions are run on main thread, so another call is necessary for pre-load apply actions

        Assert.That(assetHandle.IsLoaded);
        Assert.That(callCountAction, Is.EqualTo(1));
        Assert.That(callCountFnPtr.Value, Is.EqualTo(1));
    }

    [Test]
    public async Task ApplyAsyncAsset_DuringLoad()
    {
        int callCountAction = 0;
        StrongBox<int> callCountFnPtr = new(0);
        var assetInfo = new ManualGlobalAsset.Info(tcsOptions, 42);
        using var assetHandle = globalRegistry.Load(assetInfo, AssetLoadPriority.High, null);

        await assetInfo.WasStarted.Task;

        assetHandle.Apply(_ => callCountAction++);
        ApplyIncrementInteger(assetHandle, callCountFnPtr); // we cannot use unsafe but can call unsafe functions...

        assetInfo.Complete();
        await (globalRegistry as IAssetRegistry).WaitAsyncAll(assetHandle);
        globalRegistry.ApplyAssets();

        Assert.That(assetHandle.IsLoaded);
        Assert.That(callCountAction, Is.EqualTo(1));
        Assert.That(callCountFnPtr.Value, Is.EqualTo(1));
    }

    [Test]
    public async Task ApplyAsyncAsset_AfterLoad()
    {
        int callCountAction = 0;
        StrongBox<int> callCountFnPtr = new(0);
        var assetInfo = new ManualGlobalAsset.Info(tcsOptions, 42);
        using var assetHandle = globalRegistry.Load(assetInfo, AssetLoadPriority.High, null);

        assetInfo.Complete();
        await (globalRegistry as IAssetRegistry).WaitAsyncAll(assetHandle);
        Assert.That(assetHandle.IsLoaded);

        assetHandle.Apply(_ => callCountAction++);
        Assert.That(callCountAction, Is.EqualTo(1)); // if the asset was loaded, apply actions are to be run synchronously

        ApplyIncrementInteger(assetHandle, callCountFnPtr);
        Assert.That(callCountFnPtr.Value, Is.EqualTo(1));
    }

    [Test]
    public async Task ApplyAsyncAsset_StoredLow()
    {
        const int Low = 1, Sync = 2;
        var actions = new List<int>(2);
        var assetInfo = new ManualGlobalAsset.Info(tcsOptions, 42);
        assetInfo.Complete();

        using var assetHandleLow = globalRegistry.Load(assetInfo, AssetLoadPriority.Low, _ => actions.Add(Low));
        using var assetHandleSync = globalRegistry.Load(assetInfo, AssetLoadPriority.Synchronous, _ => actions.Add(Sync));

        Assert.That(actions, Is.EquivalentTo(new int[] { Low, Sync }));

        globalRegistry.ApplyAssets(); // provoke the registry to do something stupid
        await (globalRegistry as IAssetRegistry).WaitAsyncAll(assetHandleLow);

        Assert.That(actions, Is.EquivalentTo(new int[] { Low, Sync }));
    }

    [Test]
    public async Task SecondaryAssets_SingleSync()
    {
        var assetInfoPrimary = new ManualGlobalAsset.Info(tcsOptions, 42);
        var assetInfoSecondary = new ManualGlobalAsset.Info(tcsOptions, 1337);
        assetInfoSecondary.Complete();

        using var assetHandlePrimary = globalRegistry.Load(assetInfoPrimary, AssetLoadPriority.High, null);

        await assetInfoPrimary.WasStarted.Task;
        var assetHandleSecondary = globalRegistry.Load(assetInfoSecondary, AssetLoadPriority.Synchronous, null);
        assetInfoPrimary.Complete(assetHandleSecondary);
        await (globalRegistry as IAssetRegistry).WaitAsyncAll(assetHandlePrimary);

        Assert.That(assetHandlePrimary.IsLoaded);
        Assert.That(assetHandleSecondary.IsLoaded);
    }

    [Test]
    public async Task SecondaryAssets_SingleHigh()
    {
        var assetInfoPrimary = new ManualGlobalAsset.Info(tcsOptions, 42);
        var assetInfoSecondary = new ManualGlobalAsset.Info(tcsOptions, 1337);
        assetInfoSecondary.Complete();

        using var assetHandlePrimary = globalRegistry.Load(assetInfoPrimary, AssetLoadPriority.High, null);

        await assetInfoPrimary.WasStarted.Task;
        var assetHandleSecondary = globalRegistry.Load(assetInfoSecondary, AssetLoadPriority.High, null);
        assetInfoPrimary.Complete(assetHandleSecondary);
        await (globalRegistry as IAssetRegistry).WaitAsyncAll(assetHandlePrimary);

        Assert.That(assetHandlePrimary.IsLoaded);
        Assert.That(assetHandleSecondary.IsLoaded);
    }

    [Test]
    public async Task SecondaryAssets_SingleLow()
    {
        var assetInfoPrimary = new ManualGlobalAsset.Info(tcsOptions, 42);
        var assetInfoSecondary = new ManualGlobalAsset.Info(tcsOptions, 1337);
        assetInfoSecondary.Complete();

        using var assetHandlePrimary = globalRegistry.Load(assetInfoPrimary, AssetLoadPriority.High, null);

        await assetInfoPrimary.WasStarted.Task;
        var assetHandleSecondary = globalRegistry.Load(assetInfoSecondary, AssetLoadPriority.Low, null);
        assetInfoPrimary.Complete(assetHandleSecondary);
        globalRegistry.ApplyAssets(); // necessary to start secondary loading 
        await (globalRegistry as IAssetRegistry).WaitAsyncAll(assetHandlePrimary);

        Assert.That(assetHandlePrimary.IsLoaded);
        Assert.That(assetHandleSecondary.IsLoaded);
    }

    [Test]
    public async Task SecondaryAssets_MultipleSync()
    {
        var assetInfoPrimary = new ManualGlobalAsset.Info(tcsOptions, 42);
        var assetInfoSecondary1 = new ManualGlobalAsset.Info(tcsOptions, 1337);
        var assetInfoSecondary2 = new ManualGlobalAsset.Info(tcsOptions, 1338);
        assetInfoSecondary1.Complete();
        assetInfoSecondary2.Complete();

        using var assetHandlePrimary = globalRegistry.Load(assetInfoPrimary, AssetLoadPriority.High, null);

        await assetInfoPrimary.WasStarted.Task;
        var assetHandleSecondary1 = globalRegistry.Load(assetInfoSecondary1, AssetLoadPriority.Synchronous, null);
        var assetHandleSecondary2 = globalRegistry.Load(assetInfoSecondary2, AssetLoadPriority.Synchronous, null);
        assetInfoPrimary.Complete([assetHandleSecondary1, assetHandleSecondary2]);
        await (globalRegistry as IAssetRegistry).WaitAsyncAll(assetHandlePrimary);

        Assert.That(assetHandlePrimary.IsLoaded);
        Assert.That(assetHandleSecondary1.IsLoaded);
        Assert.That(assetHandleSecondary2.IsLoaded);
    }

    [Test]
    public async Task SecondaryAssets_PartiallyAsync()
    {
        var assetInfoPrimary = new ManualGlobalAsset.Info(tcsOptions, 42);
        var assetInfoSecondary1 = new ManualGlobalAsset.Info(tcsOptions, 1337);
        var assetInfoSecondary2 = new ManualGlobalAsset.Info(tcsOptions, 1338);
        assetInfoSecondary1.Complete();

        using var assetHandlePrimary = globalRegistry.Load(assetInfoPrimary, AssetLoadPriority.High, null);

        await assetInfoPrimary.WasStarted.Task;
        var assetHandleSecondary1 = globalRegistry.Load(assetInfoSecondary1, AssetLoadPriority.Synchronous, null);
        var assetHandleSecondary2 = globalRegistry.Load(assetInfoSecondary2, AssetLoadPriority.High, null);
        assetInfoPrimary.Complete([assetHandleSecondary1, assetHandleSecondary2]);
        assetInfoSecondary2.Complete();
        await (globalRegistry as IAssetRegistry).WaitAsyncAll(assetHandlePrimary);

        Assert.That(assetHandlePrimary.IsLoaded);
        Assert.That(assetHandleSecondary1.IsLoaded);
        Assert.That(assetHandleSecondary2.IsLoaded);
    }

    [Test]
    public async Task SecondaryAssets_FullyAsync()
    {
        var assetInfoPrimary = new ManualGlobalAsset.Info(tcsOptions, 42);
        var assetInfoSecondary1 = new ManualGlobalAsset.Info(tcsOptions, 1337);
        var assetInfoSecondary2 = new ManualGlobalAsset.Info(tcsOptions, 1338);

        using var assetHandlePrimary = globalRegistry.Load(assetInfoPrimary, AssetLoadPriority.High, null);

        await assetInfoPrimary.WasStarted.Task;
        var assetHandleSecondary1 = globalRegistry.Load(assetInfoSecondary1, AssetLoadPriority.High, null);
        var assetHandleSecondary2 = globalRegistry.Load(assetInfoSecondary2, AssetLoadPriority.High, null);
        assetInfoPrimary.Complete([assetHandleSecondary1, assetHandleSecondary2]);
        assetInfoSecondary2.Complete();
        assetInfoSecondary1.Complete(); // reverse completion order, why not.
        await (globalRegistry as IAssetRegistry).WaitAsyncAll(assetHandlePrimary);

        Assert.That(assetHandlePrimary.IsLoaded);
        Assert.That(assetHandleSecondary1.IsLoaded);
        Assert.That(assetHandleSecondary2.IsLoaded);
    }

    [Test]
    public async Task SecondaryAssets_HighNoWait()
    {
        var assetInfoPrimary = new ManualGlobalAsset.Info(tcsOptions, 42, waitForSecondary: false);
        var assetInfoSecondary1 = new ManualGlobalAsset.Info(tcsOptions, 1337);
        var assetInfoSecondary2 = new ManualGlobalAsset.Info(tcsOptions, 1338);

        using var assetHandlePrimary = globalRegistry.Load(assetInfoPrimary, AssetLoadPriority.High, null);

        await assetInfoPrimary.WasStarted.Task;
        var assetHandleSecondary1 = globalRegistry.Load(assetInfoSecondary1, AssetLoadPriority.High, null);
        var assetHandleSecondary2 = globalRegistry.Load(assetInfoSecondary2, AssetLoadPriority.High, null);
        assetInfoPrimary.Complete([assetHandleSecondary1, assetHandleSecondary2]);
        await (globalRegistry as IAssetRegistry).WaitAsyncAll(assetHandlePrimary);

        Assert.That(assetHandlePrimary.IsLoaded);
        Assert.That(assetHandleSecondary1.IsLoaded, Is.False);
        Assert.That(assetHandleSecondary2.IsLoaded, Is.False);

        assetInfoSecondary1.Complete(); // I don't want to task cancellation behavior in *this* test
        assetInfoSecondary2.Complete();
    }

    [Test]
    public async Task SecondaryAssets_LowNoWait()
    {
        var assetInfoPrimary = new ManualGlobalAsset.Info(tcsOptions, 42, waitForSecondary: false);
        var assetInfoSecondary1 = new ManualGlobalAsset.Info(tcsOptions, 1337);
        var assetInfoSecondary2 = new ManualGlobalAsset.Info(tcsOptions, 1338);

        using var assetHandlePrimary = globalRegistry.Load(assetInfoPrimary, AssetLoadPriority.High, null);

        await assetInfoPrimary.WasStarted.Task;
        var assetHandleSecondary1 = globalRegistry.Load(assetInfoSecondary1, AssetLoadPriority.Low, null);
        var assetHandleSecondary2 = globalRegistry.Load(assetInfoSecondary2, AssetLoadPriority.Low, null);
        assetInfoPrimary.Complete([assetHandleSecondary1, assetHandleSecondary2]);
        await (globalRegistry as IAssetRegistry).WaitAsyncAll(assetHandlePrimary);

        Assert.That(assetHandlePrimary.IsLoaded);
        Assert.That(assetHandleSecondary1.IsLoaded, Is.False);
        Assert.That(assetHandleSecondary2.IsLoaded, Is.False);
    }

    [Test]
    public async Task SecondaryAssets_TransitiveWait()
    {
        var assetInfoPrimary = new ManualGlobalAsset.Info(tcsOptions, 1);
        var assetInfoSecondary = new ManualGlobalAsset.Info(tcsOptions, 2);
        var assetInfoTertiary = new ManualGlobalAsset.Info(tcsOptions, 3);
        var assetInfoQuaternary = new ManualGlobalAsset.Info(tcsOptions, 4);

        using var assetHandlePrimary = globalRegistry.Load(assetInfoPrimary, AssetLoadPriority.High, null);
        using var assetHandleSecondary = globalRegistry.Load(assetInfoSecondary, AssetLoadPriority.High, null);
        using var assetHandleTertiary = globalRegistry.Load(assetInfoTertiary, AssetLoadPriority.High, null);
        using var assetHandleQuaternary = globalRegistry.Load(assetInfoQuaternary, AssetLoadPriority.High, null);

        assetInfoPrimary.Complete([assetHandleSecondary]);
        assetInfoSecondary.Complete([assetHandleTertiary]);
        assetInfoTertiary.Complete([assetHandleQuaternary]);
        assetInfoQuaternary.Complete();
        await (globalRegistry as IAssetRegistry).WaitAsyncAll(assetHandlePrimary);

        Assert.That(assetHandlePrimary.IsLoaded);
        Assert.That(assetHandleSecondary.IsLoaded);
        Assert.That(assetHandleTertiary.IsLoaded);
        Assert.That(assetHandleQuaternary.IsLoaded);
    }

    [Test, Ignore("Recursive waits are broken and NUnit does not report this well at the moment")]
    public async Task SecondaryAssets_RecursiveWait()
    {
        var assetInfoPrimary = new ManualGlobalAsset.Info(tcsOptions, 1);
        var assetInfoSecondary = new ManualGlobalAsset.Info(tcsOptions, 2);
        var assetInfoTertiary = new ManualGlobalAsset.Info(tcsOptions, 3);

        using var assetHandlePrimary = globalRegistry.Load(assetInfoPrimary, AssetLoadPriority.High, null);
        using var assetHandleSecondary = globalRegistry.Load(assetInfoSecondary, AssetLoadPriority.High, null);
        using var assetHandleTertiary = globalRegistry.Load(assetInfoTertiary, AssetLoadPriority.High, null);

        assetInfoPrimary.Complete([assetHandleSecondary]);
        assetInfoSecondary.Complete([assetHandleTertiary]);
        assetInfoTertiary.Complete([assetHandlePrimary]);

        await Assert.ThatAsync(
            async () => await (globalRegistry as IAssetRegistry).WaitAsyncAll(assetHandlePrimary),
            Throws.InvalidOperationException);

        Assert.That(assetHandlePrimary.IsLoaded, Is.False);
        Assert.That(assetHandleSecondary.IsLoaded, Is.False);
        Assert.That(assetHandleTertiary.IsLoaded, Is.False);
    }

    [Test]
    public void Error_PrimarySync()
    {
        var applyActionCount = 0;
        var assetInfo = new ManualGlobalAsset.Info(tcsOptions, 1);
        assetInfo.Fail();

        Assert.That(() =>
        {
            using var assetHandle = globalRegistry.Load(assetInfo, AssetLoadPriority.Synchronous, _ => applyActionCount++);
        }, Throws.InstanceOf<IOException>());

        Assert.That(applyActionCount, Is.Zero);
        Assert.That(assetInfo.WasUnloaded.Task.IsCompletedSuccessfully);
    }

    [Test]
    public async Task Error_PrimaryHigh()
    {
        var applyActionCount = 0;
        var assetInfo = new ManualGlobalAsset.Info(tcsOptions, 1);

        using var assetHandle = globalRegistry.Load(assetInfo, AssetLoadPriority.High, _ => applyActionCount++);
        assetInfo.Fail();
        await Assert.ThatAsync(
            () => (globalRegistry as IAssetRegistry).WaitAsyncAll(assetHandle),
            Throws.InstanceOf<AggregateException>());
        
        Assert.That(assetHandle.IsLoaded, Is.False);
        Assert.That(applyActionCount, Is.Zero);
        Assert.That(assetInfo.WasUnloaded.Task.IsCompletedSuccessfully);
    }

    [Test]
    public async Task Error_PrimaryLow()
    {
        var applyActionCount = 0;
        var assetInfo = new ManualGlobalAsset.Info(tcsOptions, 1);

        using var assetHandle = globalRegistry.Load(assetInfo, AssetLoadPriority.Low, _ => applyActionCount++);
        globalRegistry.ApplyAssets();
        assetInfo.Fail();
        await Assert.ThatAsync(
            () => (globalRegistry as IAssetRegistry).WaitAsyncAll(assetHandle),
            Throws.InstanceOf<AggregateException>());

        Assert.That(assetHandle.IsLoaded, Is.False);
        Assert.That(applyActionCount, Is.Zero);
        Assert.That(assetInfo.WasUnloaded.Task.IsCompletedSuccessfully);
    }

    // testing SyncSecondarySync does not make much sense. As the secondary load would have to be before
    // loading primary the test case just degrades to Error_PrimarySync.
    // We test transitive errors in any other of these Error_*Secondary* tests

    [Test]
    public void Error_SyncSecondaryHigh()
    {
        var assetInfoPrimary = new ManualGlobalAsset.Info(tcsOptions, 1);
        var assetInfoSecondary = new ManualGlobalAsset.Info(tcsOptions, 2);
        assetInfoSecondary.Fail();

        using var assetHandleSecondary = globalRegistry.Load(assetInfoSecondary, AssetLoadPriority.High, null);
        assetInfoPrimary.Complete([assetHandleSecondary]);
        Assert.That(() =>
        {
            using var assetHandlePrimary = globalRegistry.Load(assetInfoPrimary, AssetLoadPriority.Synchronous, null);
        }, Throws.InstanceOf<AggregateException>()); // aggregate because we will always use Task.WhenAll to wait for secondaries

        Assert.That(assetHandleSecondary.IsLoaded, Is.False);
        Assert.That(assetInfoPrimary.WasUnloaded.Task.IsCompletedSuccessfully);
        Assert.That(assetInfoSecondary.WasUnloaded.Task.IsCompletedSuccessfully);
    }

    [Test]
    public void Error_SyncSecondaryLow()
    {
        var assetInfoPrimary = new ManualGlobalAsset.Info(tcsOptions, 1);
        var assetInfoSecondary = new ManualGlobalAsset.Info(tcsOptions, 2);
        assetInfoSecondary.Fail();

        using var assetHandleSecondary = globalRegistry.Load(assetInfoSecondary, AssetLoadPriority.Low, null);
        assetInfoPrimary.Complete([assetHandleSecondary]);
        globalRegistry.ApplyAssets();
        Assert.That(() =>
        {
            using var assetHandlePrimary = globalRegistry.Load(assetInfoPrimary, AssetLoadPriority.Synchronous, null);
        }, Throws.InstanceOf<AggregateException>()); // aggregate because we will always use Task.WhenAll to wait for secondaries

        Assert.That(assetHandleSecondary.IsLoaded, Is.False);
        Assert.That(assetInfoPrimary.WasUnloaded.Task.IsCompletedSuccessfully);
        Assert.That(assetInfoSecondary.WasUnloaded.Task.IsCompletedSuccessfully);
    }

    [Test]
    public async Task Error_HighSecondarySync()
    {
        var assetInfoPrimary = new ManualGlobalAsset.Info(tcsOptions, 1);
        var assetInfoSecondary = new ManualGlobalAsset.Info(tcsOptions, 2);
        assetInfoSecondary.Fail();
        
        using var assetHandlePrimary = globalRegistry.Load(assetInfoPrimary, AssetLoadPriority.High, null);
        await assetInfoPrimary.WasStarted.Task;
        try
        {
            using var assetHandleSecondary = globalRegistry.Load(assetInfoSecondary, AssetLoadPriority.Synchronous, null);
            Assert.Fail("Expected the secondary asset load to throw an exception");
        }
        catch(Exception ex) // we do not have a handle to the secondary, but the exception would be thrown in the Load method of the primary 
        {
            assetInfoPrimary.Completion.SetException(ex);
        }

        await Assert.ThatAsync(
            () => (globalRegistry as IAssetRegistry).WaitAsyncAll(assetHandlePrimary),
            Throws.InstanceOf<AggregateException>());

        Assert.That(assetHandlePrimary.IsLoaded, Is.False);
        Assert.That(assetInfoPrimary.WasUnloaded.Task.IsCompletedSuccessfully);
        Assert.That(assetInfoSecondary.WasUnloaded.Task.IsCompletedSuccessfully);
    }

    [Test]
    public async Task Error_HighSecondaryHigh()
    {
        var assetInfoPrimary = new ManualGlobalAsset.Info(tcsOptions, 1);
        var assetInfoSecondary = new ManualGlobalAsset.Info(tcsOptions, 2);

        using var assetHandlePrimary = globalRegistry.Load(assetInfoPrimary, AssetLoadPriority.High, null);
        await assetInfoPrimary.WasStarted.Task;
        using var assetHandleSecondary = globalRegistry.Load(assetInfoSecondary, AssetLoadPriority.High, null);
        assetInfoPrimary.Complete([assetHandleSecondary]);
        await assetInfoSecondary.WasStarted.Task;
        assetInfoSecondary.Fail();

        await Assert.ThatAsync(
            () => (globalRegistry as IAssetRegistry).WaitAsyncAll(assetHandlePrimary),
            Throws.InstanceOf<AggregateException>());

        Assert.That(assetHandlePrimary.IsLoaded, Is.False);
        Assert.That(assetHandleSecondary.IsLoaded, Is.False);
        Assert.That(assetInfoPrimary.WasUnloaded.Task.IsCompletedSuccessfully);
        Assert.That(assetInfoSecondary.WasUnloaded.Task.IsCompletedSuccessfully);
    }

    [Test]
    public async Task Error_HighSecondaryLow()
    {
        var assetInfoPrimary = new ManualGlobalAsset.Info(tcsOptions, 1);
        var assetInfoSecondary = new ManualGlobalAsset.Info(tcsOptions, 2);

        using var assetHandlePrimary = globalRegistry.Load(assetInfoPrimary, AssetLoadPriority.High, null);
        await assetInfoPrimary.WasStarted.Task;
        using var assetHandleSecondary = globalRegistry.Load(assetInfoSecondary, AssetLoadPriority.Low, null);
        assetInfoPrimary.Complete([assetHandleSecondary]);
        globalRegistry.ApplyAssets();
        await assetInfoSecondary.WasStarted.Task;
        assetInfoSecondary.Fail();

        await Assert.ThatAsync(
            () => (globalRegistry as IAssetRegistry).WaitAsyncAll(assetHandlePrimary),
            Throws.InstanceOf<AggregateException>());

        Assert.That(assetHandlePrimary.IsLoaded, Is.False);
        Assert.That(assetHandleSecondary.IsLoaded, Is.False);
        Assert.That(assetInfoPrimary.WasUnloaded.Task.IsCompletedSuccessfully);
        Assert.That(assetInfoSecondary.WasUnloaded.Task.IsCompletedSuccessfully);
    }

    [Test]
    public async Task Error_LowSecondarySync()
    {
        var assetInfoPrimary = new ManualGlobalAsset.Info(tcsOptions, 1);
        var assetInfoSecondary = new ManualGlobalAsset.Info(tcsOptions, 2);
        assetInfoSecondary.Fail();
        
        using var assetHandlePrimary = globalRegistry.Load(assetInfoPrimary, AssetLoadPriority.Low, null);
        globalRegistry.ApplyAssets();
        await assetInfoPrimary.WasStarted.Task;
        try
        {
            using var assetHandleSecondary = globalRegistry.Load(assetInfoSecondary, AssetLoadPriority.Synchronous, null);
            Assert.Fail("Expected the secondary asset load to throw an exception");
        }
        catch(Exception ex) // we do not have a handle to the secondary, but the exception would be thrown in the Load method of the primary 
        {
            assetInfoPrimary.Completion.SetException(ex);
        }

        await Assert.ThatAsync(
            () => (globalRegistry as IAssetRegistry).WaitAsyncAll(assetHandlePrimary),
            Throws.InstanceOf<AggregateException>());

        Assert.That(assetHandlePrimary.IsLoaded, Is.False);
        Assert.That(assetInfoPrimary.WasUnloaded.Task.IsCompletedSuccessfully);
        Assert.That(assetInfoSecondary.WasUnloaded.Task.IsCompletedSuccessfully);
    }

    [Test]
    public async Task Error_LowSecondaryHigh()
    {
        var assetInfoPrimary = new ManualGlobalAsset.Info(tcsOptions, 1);
        var assetInfoSecondary = new ManualGlobalAsset.Info(tcsOptions, 2);

        using var assetHandlePrimary = globalRegistry.Load(assetInfoPrimary, AssetLoadPriority.Low, null);
        globalRegistry.ApplyAssets();
        await assetInfoPrimary.WasStarted.Task;
        using var assetHandleSecondary = globalRegistry.Load(assetInfoSecondary, AssetLoadPriority.High, null);
        assetInfoPrimary.Complete([assetHandleSecondary]);
        await assetInfoSecondary.WasStarted.Task;
        assetInfoSecondary.Fail();

        await Assert.ThatAsync(
            () => (globalRegistry as IAssetRegistry).WaitAsyncAll(assetHandlePrimary),
            Throws.InstanceOf<AggregateException>());

        Assert.That(assetHandlePrimary.IsLoaded, Is.False);
        Assert.That(assetHandleSecondary.IsLoaded, Is.False);
        Assert.That(assetInfoPrimary.WasUnloaded.Task.IsCompletedSuccessfully);
        Assert.That(assetInfoSecondary.WasUnloaded.Task.IsCompletedSuccessfully);
    }

    [Test]
    public async Task Error_LowSecondaryLow()
    {
        var assetInfoPrimary = new ManualGlobalAsset.Info(tcsOptions, 1);
        var assetInfoSecondary = new ManualGlobalAsset.Info(tcsOptions, 2);

        using var assetHandlePrimary = globalRegistry.Load(assetInfoPrimary, AssetLoadPriority.Low, null);
        globalRegistry.ApplyAssets();
        await assetInfoPrimary.WasStarted.Task;
        using var assetHandleSecondary = globalRegistry.Load(assetInfoSecondary, AssetLoadPriority.Low, null);
        assetInfoPrimary.Complete([assetHandleSecondary]);
        globalRegistry.ApplyAssets();
        await assetInfoSecondary.WasStarted.Task;
        assetInfoSecondary.Fail();

        await Assert.ThatAsync(
            () => (globalRegistry as IAssetRegistry).WaitAsyncAll(assetHandlePrimary),
            Throws.InstanceOf<AggregateException>());

        Assert.That(assetHandlePrimary.IsLoaded, Is.False);
        Assert.That(assetHandleSecondary.IsLoaded, Is.False);
        Assert.That(assetInfoPrimary.WasUnloaded.Task.IsCompletedSuccessfully);
        Assert.That(assetInfoSecondary.WasUnloaded.Task.IsCompletedSuccessfully);
    }

    public async Task Error_DeadlockAssetStateAndSecondaryException()
    {
        // motivation is that we want to create a scenario where:
        //   - primary waits on one thread (mistakenly holding the asset state lock)
        //   - secondary on another thread failed and sets its completion exception
        //   - the completion continues synchronously
        //   - primary wants to sets its error and tries to lock the asset state.
        // -> deadlock

        var assetInfoPrimary = new ManualGlobalAsset.Info(tcsOptions, 1);
        var assetInfoSecondary1 = new ManualGlobalAsset.Info(tcsOptions, 2);
        var assetInfoSecondary2 = new ManualGlobalAsset.Info(tcsOptions, 3);
        assetInfoSecondary1.Fail();
        assetInfoSecondary2.Fail();

        using var assetHandlePrimary = globalRegistry.Load(assetInfoPrimary, AssetLoadPriority.Low, null);
        assetInfoPrimary.WasStarted.Task.ContinueWith(_ =>
        {
            var assetHandleSecondary1 = globalRegistry.Load(assetInfoSecondary1, AssetLoadPriority.High, null);
            var assetHandleSecondary2 = globalRegistry.Load(assetInfoSecondary2, AssetLoadPriority.High, null);
            assetInfoPrimary.Complete([assetHandleSecondary1, assetHandleSecondary2]);
        },
        TestContext.CurrentContext.CancellationToken,
        TaskContinuationOptions.ExecuteSynchronously,
        TaskScheduler.Current);
        Assert.That(() =>
        {
            assetHandlePrimary.Get<ManualGlobalAsset>();
        }, Throws.InstanceOf<AggregateException>()); // aggregate because we will always use Task.WhenAll to wait for secondaries

        Assert.That(assetInfoPrimary.WasUnloaded.Task.IsCompletedSuccessfully);
        Assert.That(assetInfoSecondary1.WasUnloaded.Task.IsCompletedSuccessfully);
        Assert.That(assetInfoSecondary2.WasUnloaded.Task.IsCompletedSuccessfully);
    }
}
