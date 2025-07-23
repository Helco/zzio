using System;
using System.Threading;
using System.Threading.Tasks;
using zzio;
using zzre.rendering;

namespace zzre;

public sealed class WorldAsset(IAssetRegistry registry, WorldAsset.Info info, WorldMesh mesh) : IAsset<WorldAsset.Info>
{
    static AssetLocality IAsset.Locality => AssetLocality.Global;    

    public readonly record struct Info(FilePath FullPath);

    public IAssetRegistry Registry { get; } = registry;
    public WorldMesh Mesh { get; private set; } = mesh;
    private readonly Info info = info;

    static Task<AssetLoadResult<Info>> IAsset<Info>.LoadAsync(IAssetRegistry registry, Guid assetId, Info info, CancellationToken ct)
    {
        var mesh = new WorldMesh(registry.DIContainer, info.FullPath);
        return Task.FromResult<AssetLoadResult<Info>>(new(
            new WorldAsset(registry, info, mesh)
        ));
    }

    public void Dispose()
    {
        Mesh?.Dispose();
        Mesh = null!;
    }

    public override string ToString() => $"World {info.FullPath.Parts[^1]}";
}

static partial class AssetExtensions
{
    public static AssetHandle<WorldAsset> LoadWorld(this IAssetRegistry registry,
        FilePath path,
        AssetPriority priority) =>
        registry.Load<WorldAsset.Info, WorldAsset>(new(path), priority);
}
