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
    public void LoadSynchronousGlobalAsset()
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
}
