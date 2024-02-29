using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using zzio;
using zzio.vfs;

namespace zzre;

public sealed class AnimationAsset : Asset
{
    private static readonly FilePath BasePath = new("resources/models/actorsex");

    public readonly record struct Info(string Name)
    {
        public FilePath FullPath => BasePath.Combine(
            Name.EndsWith(".ska", StringComparison.OrdinalIgnoreCase) ? Name : Name + ".ska");
    }

    public static void Register() =>
        AssetInfoRegistry<Info>.Register<AnimationAsset>();

    private readonly Info info;
    private SkeletalAnimation? animation;

    public SkeletalAnimation Animation => animation ??
        throw new InvalidOperationException("Asset was not yet loaded");

    public AnimationAsset(IAssetRegistry registry, Guid assetId, Info info) : base(registry, assetId)
    {
        this.info = info;
    }

    protected override ValueTask<IEnumerable<AssetHandle>> Load()
    {
        var resourcePool = diContainer.GetTag<IResourcePool>();
        using var stream = resourcePool.FindAndOpen(info.FullPath) ??
            throw new System.IO.FileNotFoundException($"Could not find animation: {info.Name}");
        animation = SkeletalAnimation.ReadNew(stream);
        return NoSecondaryAssets;
    }

    protected override void Unload()
    {
        animation = null;
    }

    public override string ToString() => $"Animation {info.Name}";
}
