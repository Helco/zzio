using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using zzio;
using zzre.rendering;

namespace zzre;

public sealed class ClumpAsset : Asset
{
    private static readonly FilePath BasePath = new("resources/models/");

    public readonly record struct Info(
        string Directory,
        string Name)
    {
        public static Info Model(string name) => new("models", name);
        public static Info Actor(string name) => new("actorsex", name);
        public static Info Backdrop(string name) => new("backdrops", name);

        public FilePath FullPath => BasePath.Combine(Directory, Name + ".dff");
    }

    public static void RegisterAt(AssetRegistry registry) =>
        registry.RegisterAssetType<Info, ClumpAsset>();

    private readonly Info info;
    private ClumpMesh? mesh;

    public ClumpMesh Mesh => mesh ??
        throw new InvalidOperationException("Asset was not yet loaded");

    public ClumpAsset(ITagContainer diContainer, Guid assetId, Info info) : base(diContainer, assetId)
    {
        this.info = info;
    }

    protected override ValueTask<IEnumerable<AssetHandle>> Load()
    {
        mesh = new ClumpMesh(diContainer, info.FullPath);
        return ValueTask.FromResult(Enumerable.Empty<AssetHandle>());
    }

    protected override void Unload()
    {
        mesh?.Dispose();
        mesh = null;
    }
}

public static unsafe partial class AssetExtensions
{
    public static AssetHandle LoadClump(this IAssetHandleScope assetScope,
        DefaultEcs.Entity entity,
        ClumpAsset.Info info,
        AssetLoadPriority priority)
    {
        var handle = assetScope.Load(info, priority, &ApplyClumpToEntity, entity);
        entity.Set(handle);
        return handle;
    }

    private static void ApplyClumpToEntity(AssetHandle handle, ref readonly DefaultEcs.Entity entity)
    {
        var asset = handle.Get<ClumpAsset>();
        entity.Set(asset.Mesh);
    }
}
