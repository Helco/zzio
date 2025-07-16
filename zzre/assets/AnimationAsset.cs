using System;
using System.Threading;
using System.Threading.Tasks;
using zzio;
using zzio.vfs;

namespace zzre;

public sealed class AnimationAsset : IAsset<AnimationAsset.Info>
{
    private static readonly FilePath BasePath = new("resources/models/actorsex");

    public readonly record struct Info(string Name)
    {
        public FilePath FullPath => BasePath.Combine(
            Name.EndsWith(".ska", StringComparison.OrdinalIgnoreCase) ? Name : Name + ".ska");
    }

    private readonly Info info;

    public IAssetRegistry Registry { get; }
    public SkeletalAnimation Animation { get; private set; }

    private AnimationAsset(IAssetRegistry registry, Info info, SkeletalAnimation animation)
    {
        this.info = info;
        Registry = registry;
        Animation = animation;
    }

    static Task<AssetLoadResult<Info>> IAsset<Info>.LoadAsync(IAssetRegistry registry, Info info, CancellationToken ct)
    {
        var resourcePool = registry.DIContainer.GetTag<IResourcePool>();
        using var stream = resourcePool.FindAndOpen(info.FullPath) ??
            throw new System.IO.FileNotFoundException($"Could not find animation: {info.Name}");
        var animation = SkeletalAnimation.ReadNew(stream);
        return Task.FromResult(new AssetLoadResult<Info>(new AnimationAsset(registry, info, animation)));
    }

    public override string ToString() => $"Animation {info.Name}";
}

static partial class AssetExtensions
{
    public static AssetHandle<AnimationAsset> LoadAnimation(this IAssetRegistry registry, string name, AssetPriority priority) =>
        registry.Load<AnimationAsset.Info, AnimationAsset>(new(name), priority);
}
