using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualBasic;
using NUnit.Framework;
using NUnit.Framework.Constraints;
using NUnit.Framework.Internal;

namespace zzre.tests;

[TestFixture, Apartment(System.Threading.ApartmentState.STA)]
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

    static TestAssetRegistry()
    {
        AssetInfoRegistry<SynchronousGlobalAsset.Info>.Register<SynchronousGlobalAsset>(AssetLocality.Global);
        AssetInfoRegistry<SynchronousContextAsset.Info>.Register<SynchronousContextAsset>(AssetLocality.Context);
        AssetInfoRegistry<SynchronousSingleUsageAsset.Info>.Register<SynchronousSingleUsageAsset>(AssetLocality.SingleUsage);
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
}
