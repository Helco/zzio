using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using zzio;
using zzio.effect;
using zzio.vfs;

namespace zzre;

public sealed class EffectCombinerAsset : Asset
{
    private static readonly FilePath BasePath = new("resources/effects/");
    
    public readonly record struct Info(string Name)
    {
        public readonly FilePath FullPath => BasePath.Combine(
            Name.EndsWith(".ed", StringComparison.OrdinalIgnoreCase) ? Name : Name + ".ed");
    }

    public static void Register() =>
        AssetInfoRegistry<Info>.Register<EffectCombinerAsset>(AssetLocality.Global);

    private readonly Info info;
    private EffectCombiner? effectCombiner;

    public EffectCombiner EffectCombiner => effectCombiner ??
        throw new InvalidOperationException("Asset was not yet loaded");

    public EffectCombinerAsset(IAssetRegistry registry, Guid assetId, Info info) : base(registry, assetId)
    {
        this.info = info;
    }

    protected override ValueTask<IEnumerable<AssetHandle>> Load()
    {
        var resourcePool = diContainer.GetTag<IResourcePool>();
        using var stream = resourcePool.FindAndOpen(info.FullPath) ??
            throw new System.IO.FileNotFoundException($"Could not find effect combiner: {info.Name}");
        effectCombiner = new();
        effectCombiner.Read(stream);
        return NoSecondaryAssets;
    }

    protected override void Unload()
    {
        effectCombiner = null;
    }

    public override string ToString() => $"EffectCombiner {info.Name}";
}

partial class AssetExtensions
{
    public unsafe static AssetHandle<EffectCombinerAsset> LoadEffectCombiner(this IAssetRegistry registry,
        DefaultEcs.Entity entity,
        string name,
        AssetLoadPriority priority)
    {
        var handle = registry.Load(new EffectCombinerAsset.Info(name), priority, &ApplyEffectCombinerToEntity, entity);
        entity.Set(handle);
        return handle.As<EffectCombinerAsset>();
    }

    private static void ApplyEffectCombinerToEntity(AssetHandle handle, ref readonly DefaultEcs.Entity entity)
    {
        if (entity.IsAlive)
            entity.Set(handle.Get<EffectCombinerAsset>().EffectCombiner);
    }
}
