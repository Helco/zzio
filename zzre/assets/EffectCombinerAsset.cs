using System;
using System.Threading;
using System.Threading.Tasks;
using zzio;
using zzio.effect;
using zzio.vfs;

namespace zzre;

public sealed class EffectCombinerAsset(IAssetRegistry registry, EffectCombinerAsset.Info info, EffectCombiner effect) : IAsset<EffectCombinerAsset.Info>
{
    static AssetLocality IAsset.Locality => AssetLocality.Global;

    public readonly record struct Info(FilePath FullPath)
    {
        public readonly string Name => FullPath.Parts[^1];
    }

    private readonly Info info = info;
    public IAssetRegistry Registry { get; } = registry;
    public EffectCombiner EffectCombiner { get; private set; } = effect;

    static Task<AssetLoadResult<Info>> IAsset<Info>.LoadAsync(IAssetRegistry registry, Guid assetId, Info info, CancellationToken ct)
    {
        var resourcePool = registry.DIContainer.GetTag<IResourcePool>();
        using var stream = resourcePool.FindAndOpen(info.FullPath) ??
            throw new System.IO.FileNotFoundException($"Could not find effect combiner: {info.Name}");
        var effectCombiner = new EffectCombiner();
        effectCombiner.Read(stream);
        return Task.FromResult<AssetLoadResult<Info>>(new(
            new EffectCombinerAsset(registry, info, effectCombiner)
        ));
    }

    public void Dispose()
    {
        EffectCombiner = null!;    
    }

    public override string ToString() => $"EffectCombiner {info.Name}";
}

partial class AssetExtensions
{
    public static AssetHandle<EffectCombinerAsset> LoadEffectCombiner(this IAssetRegistry registry,
        FilePath fullPath,
        AssetPriority priority = AssetPriority.Synchronous) =>
        registry.Load<EffectCombinerAsset.Info, EffectCombinerAsset>(new(fullPath), priority);
}
