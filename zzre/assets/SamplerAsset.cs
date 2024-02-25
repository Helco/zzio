using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Veldrid;

namespace zzre;

public sealed class SamplerAsset : Asset
{
    public static void Register() =>
        AssetInfoRegistry<SamplerDescription>.Register<SamplerAsset>();

    private readonly SamplerDescription info;
    private Sampler? sampler;

    public Sampler Sampler => sampler ??
        throw new InvalidOperationException("Asset was not yet loaded");

    public SamplerAsset(ITagContainer diContainer, Guid assetId, SamplerDescription info) : base(diContainer, assetId)
    {
        this.info = info;
    }

    protected override ValueTask<IEnumerable<AssetHandle>> Load()
    {
        var resourceFactory = diContainer.GetTag<ResourceFactory>();
        sampler = resourceFactory.CreateSampler(info);
        return NoSecondaryAssets;
    }

    protected override void Unload()
    {
        sampler?.Dispose();
        sampler = null;
    }
}

public static unsafe partial class AssetExtensions
{
    public static AssetHandle<SamplerAsset> LoadSampler(this IAssetRegistry registry, in SamplerDescription info) =>
        registry.Load(info, AssetLoadPriority.Synchronous).As<SamplerAsset>();
}
