using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using NUnit.Framework.Constraints;

namespace zzre.tests;

[TestFixture, Apartment(System.Threading.ApartmentState.STA), CancelAfter(1000)]
public class TestAssetRegistry
{
    class SynchronousAsset : Asset
    {
        public SynchronousAsset(IAssetRegistry registry, Guid id) : base(registry, id)
        {
        }

        protected override ValueTask<IEnumerable<AssetHandle>> Load()
        {
            return ValueTask.FromResult(Enumerable.Empty<AssetHandle>());
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

        public class Info(int id) : IEquatable<Info>
        {
            public readonly int ID = id;
            public readonly TaskCompletionSource Completion = new();
            public readonly TaskCompletionSource WasStarted = new();
            public readonly TaskCompletionSource WasUnloaded = new();

            bool IEquatable<Info>.Equals(Info? other) => ID == other?.ID;
        }

        public int InfoID => info.ID;
        private readonly Info info;

        protected override ValueTask<IEnumerable<AssetHandle>> Load()
        {
            info.WasStarted.SetResult(); // throws if we enter twice. Good.
            return new(info.Completion.Task.ContinueWith(_ => Enumerable.Empty<AssetHandle>()));
        }

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

    [SetUp]
    public void Setup()
    {
        diContainer = new();
        diContainer.AddTag<Serilog.ILogger>(Serilog.Core.Logger.None);
        diContainer.AddTag<IAssetRegistry>(globalRegistry = new AssetRegistry("Global", diContainer));
        localRegistry = new AssetLocalRegistry("Local", diContainer);
        TestContext.CurrentContext.CancellationToken.Register(localRegistry.Dispose);
        TestContext.CurrentContext.CancellationToken.Register(globalRegistry.Dispose);
    }

    [TearDown]
    public void TearDown()
    {
        localRegistry.Dispose();
        diContainer.Dispose();
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

    private static void IncrementInteger(AssetHandle assetHandle, in StrongBox<int> callCount)
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
            &IncrementInteger, callCount);
        Assert.That(callCount.Value, Is.EqualTo(1));
    }

    [Test]
    public unsafe void ApplySyncAsset_ApplyAfterLoad()
    {
        StrongBox<int> callCountFnPtr = new(0);
        using var assetHandle1 = globalRegistry.Load(new SynchronousGlobalAsset.Info(42), AssetLoadPriority.Synchronous,
            &IncrementInteger, callCountFnPtr);
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
        assetHandle.Apply(&IncrementInteger, callCountFnPtr);
        Assert.That(callCountFnPtr.Value, Is.EqualTo(1));

        int callCountAction = 0;
        assetHandle.Apply(_ => callCountAction++);
        Assert.That(callCountAction, Is.EqualTo(1));

        Assert.That(callCountFnPtr.Value, Is.EqualTo(1));
    }

    [Test]
    public void LoadAsyncGlobalAsset_HighSync()
    {
        var assetInfo = new ManualGlobalAsset.Info(42);
        using var assetHandle = globalRegistry.Load(assetInfo, AssetLoadPriority.High, null);
        Assert.That(assetHandle.IsLoaded, Is.False);
        // we cannot reason about WasStarted, as it is called asynchronously
        Assert.That(assetInfo.WasUnloaded.Task.IsCompleted, Is.False);

        assetInfo.Completion.SetResult();
        var asset = assetHandle.Get<ManualGlobalAsset>();

        Assert.That(assetHandle.IsLoaded, Is.True);
        Assert.That(assetInfo.WasStarted.Task.IsCompletedSuccessfully, Is.True);
        Assert.That(assetInfo.WasUnloaded.Task.IsCompleted, Is.False);
    }

    [Test]
    public void LoadAsyncGlobalAsset_LowSync()
    {
        var assetInfo = new ManualGlobalAsset.Info(42);
        using var assetHandle = globalRegistry.Load(assetInfo, AssetLoadPriority.Low, null);
        Assert.That(assetHandle.IsLoaded, Is.False);
        Assert.That(assetInfo.WasStarted.Task.IsCompleted, Is.False);
        Assert.That(assetInfo.WasUnloaded.Task.IsCompleted, Is.False);

        globalRegistry.ApplyAssets();
        assetInfo.Completion.SetResult();
        var asset = assetHandle.Get<ManualGlobalAsset>();

        Assert.That(assetHandle.IsLoaded, Is.True);
        Assert.That(assetInfo.WasStarted.Task.IsCompletedSuccessfully, Is.True);
        Assert.That(assetInfo.WasUnloaded.Task.IsCompleted, Is.False);
    }

    [Test]
    public async Task LoadAsyncGlobalAsset_HighAsync()
    {
        var assetInfo = new ManualGlobalAsset.Info(42);
        using var assetHandle = globalRegistry.Load(assetInfo, AssetLoadPriority.High, null);
        Assert.That(assetHandle.IsLoaded, Is.False);
        // we cannot reason about WasStarted, as it is called asynchronously
        Assert.That(assetInfo.WasUnloaded.Task.IsCompleted, Is.False);

        assetInfo.Completion.SetResult();
        await (globalRegistry as IAssetRegistry).WaitAsyncAll(assetHandle);

        Assert.That(assetHandle.IsLoaded, Is.True);
        Assert.That(assetInfo.WasStarted.Task.IsCompletedSuccessfully, Is.True);
        Assert.That(assetInfo.WasUnloaded.Task.IsCompleted, Is.False);
    }

    [Test]
    public async Task LoadAsyncGlobalAsset_LowAsync()
    {
        var assetInfo = new ManualGlobalAsset.Info(42);
        using var assetHandle = globalRegistry.Load(assetInfo, AssetLoadPriority.Low, null);
        Assert.That(assetHandle.IsLoaded, Is.False);
        Assert.That(assetInfo.WasStarted.Task.IsCompleted, Is.False);
        Assert.That(assetInfo.WasUnloaded.Task.IsCompleted, Is.False);

        globalRegistry.ApplyAssets();
        assetInfo.Completion.SetResult();
        await (globalRegistry as IAssetRegistry).WaitAsyncAll(assetHandle);

        Assert.That(assetHandle.IsLoaded, Is.True);
        Assert.That(assetInfo.WasStarted.Task.IsCompletedSuccessfully, Is.True);
        Assert.That(assetInfo.WasUnloaded.Task.IsCompleted, Is.False);
    }

    [Test]
    public async Task UnloadAsyncGlobalAsset_HighNormal()
    {
        var assetInfo = new ManualGlobalAsset.Info(42);
        using var assetHandle = globalRegistry.Load(assetInfo, AssetLoadPriority.High, null);
        assetInfo.Completion.SetResult();
        await (globalRegistry as IAssetRegistry).WaitAsyncAll(assetHandle);
        Assert.That(assetHandle.IsLoaded, Is.True);

        assetHandle.Dispose();
        globalRegistry.ApplyAssets();

        Assert.That(assetHandle.IsLoaded, Is.False);
        Assert.That(assetInfo.WasUnloaded.Task.IsCompletedSuccessfully, Is.True);
    }

    [Test]
    public async Task UnloadAsyncGlobalAsset_LowNormal()
    {
        var assetInfo = new ManualGlobalAsset.Info(42);
        using var assetHandle = globalRegistry.Load(assetInfo, AssetLoadPriority.Low, null);
        assetInfo.Completion.SetResult();
        globalRegistry.ApplyAssets();
        await (globalRegistry as IAssetRegistry).WaitAsyncAll(assetHandle);
        Assert.That(assetHandle.IsLoaded, Is.True);

        assetHandle.Dispose();
        globalRegistry.ApplyAssets();

        Assert.That(assetHandle.IsLoaded, Is.False);
        Assert.That(assetInfo.WasUnloaded.Task.IsCompletedSuccessfully, Is.True);
    }

    [Test]
    public async Task UnloadAsyncGlobalAsset_HighDuringLoad()
    {
        var assetInfo = new ManualGlobalAsset.Info(42);
        using var assetHandle = globalRegistry.Load(assetInfo, AssetLoadPriority.High, null);
        await assetInfo.WasStarted.Task;

        assetHandle.Dispose();
        globalRegistry.ApplyAssets();
        assetInfo.Completion.SetResult();

        Assert.That(assetHandle.IsLoaded, Is.False);
        Assert.That(assetInfo.WasUnloaded.Task.IsCompletedSuccessfully, Is.True);
    }

    [Test]
    public async Task UnloadAsyncGlobalAsset_LowDuringLoad()
    {
        var assetInfo = new ManualGlobalAsset.Info(42);
        using var assetHandle = globalRegistry.Load(assetInfo, AssetLoadPriority.Low, null);
        globalRegistry.ApplyAssets();
        await assetInfo.WasStarted.Task;

        assetHandle.Dispose();
        globalRegistry.ApplyAssets();
        assetInfo.Completion.SetResult();

        Assert.That(assetHandle.IsLoaded, Is.False);
        Assert.That(assetInfo.WasUnloaded.Task.IsCompletedSuccessfully, Is.True);
    }

    [Test]
    public async Task UnloadAsyncGlobalAsset_HighBeforeLoad()
    {
        var assetInfo = new ManualGlobalAsset.Info(42);
        using var assetHandle = globalRegistry.Load(assetInfo, AssetLoadPriority.High, null);
        assetHandle.Dispose();
        if (assetInfo.WasStarted.Task.IsCompletedSuccessfully)
            // yes, this is not fully correct but also not terrible if *this* test not always succeeds
            Assert.Ignore("Asset was already started, unsynchronizable test will not be conclusive");

        assetInfo.Completion.SetResult();
        await Task.Yield();

        Assert.That(assetHandle.IsLoaded, Is.False);
        Assert.That(assetInfo.WasUnloaded.Task.IsCompletedSuccessfully, Is.True);
    }

    [Test]
    public async Task UnloadAsyncGlobalAsset_LowBeforeLoad()
    {
        var assetInfo = new ManualGlobalAsset.Info(42);
        using var assetHandle = globalRegistry.Load(assetInfo, AssetLoadPriority.Low, null);
        assetHandle.Dispose();
        globalRegistry.ApplyAssets();

        assetInfo.Completion.SetResult();
        await Task.Yield();

        Assert.That(assetHandle.IsLoaded, Is.False);
        Assert.That(assetInfo.WasUnloaded.Task.IsCompletedSuccessfully, Is.True);
    }

    [Test]
    public Task UnloadAsyncGlobalAsset_High_AccessDisposedHandle()
    {
        var assetInfo = new ManualGlobalAsset.Info(42);
        using var assetHandle = globalRegistry.Load(assetInfo, AssetLoadPriority.High, null);
        if (assetInfo.WasStarted.Task.IsCompletedSuccessfully)
            Assert.Ignore("Asset was already started, unsynchronizable test will not be conclusive");
        assetHandle.Dispose();
        globalRegistry.ApplyAssets();

        assetInfo.Completion.SetResult();
        return Assert.ThatAsync(
            () => (globalRegistry as IAssetRegistry).WaitAsyncAll(assetHandle),
            Throws.InstanceOf<ObjectDisposedException>());
    }

    [Test]
    public Task UnloadAsyncGlobalAsset_Low_AccessDisposedHandle()
    {
        var assetInfo = new ManualGlobalAsset.Info(42);
        using var assetHandle = globalRegistry.Load(assetInfo, AssetLoadPriority.Low, null);
        assetHandle.Dispose();
        globalRegistry.ApplyAssets();

        assetInfo.Completion.SetResult();
        return Assert.ThatAsync(
            () => (globalRegistry as IAssetRegistry).WaitAsyncAll(assetHandle),
            Throws.InstanceOf<ObjectDisposedException>());
    }
}
