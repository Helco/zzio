using System;
using System.Collections.Generic;
using System.Text;
using Veldrid;

namespace zzre;

public sealed class SamplerAsset : Asset
{
    public static void Register() =>
        AssetInfoRegistry<SamplerDescription>.Register<SamplerAsset>(AssetLocality.Global);

    private readonly SamplerDescription info;
    private Sampler? sampler;

    public string DebugName { get; }
    public Sampler Sampler => sampler ??
        throw new InvalidOperationException("Asset was not yet loaded");

    public SamplerAsset(IAssetRegistry registry, Guid assetId, SamplerDescription info) : base(registry, assetId)
    {
        this.info = info;
        var stringBuilder = new StringBuilder("Sampler ");
        stringBuilder.Append(info.Filter);
        stringBuilder.Append(' ');
        stringBuilder.Append(info.AddressModeU);
        if (info.AddressModeV != info.AddressModeU)
        {
            stringBuilder.Append(',');
            stringBuilder.Append(info.AddressModeV);
        }
        if (info.MaximumLod == 0)
            stringBuilder.Append(" (No LOD)");
        DebugName = stringBuilder.ToString();
    }

    protected override IEnumerable<AssetHandle> Load()
    {
        var resourceFactory = diContainer.GetTag<ResourceFactory>();
        sampler = resourceFactory.CreateSampler(info);
        sampler.Name = DebugName;
        return NoSecondaryAssets;
    }

    protected override void Unload()
    {
        sampler?.Dispose();
        sampler = null;
    }

    protected override string ToStringInner() => DebugName;
}

public static unsafe partial class AssetExtensions
{
    public static AssetHandle<SamplerAsset> LoadSampler(this IAssetRegistry registry, in SamplerDescription info) =>
        registry.Load(info, AssetLoadPriority.Synchronous).As<SamplerAsset>();
}
