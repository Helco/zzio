using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using zzio;
using zzre.rendering;

namespace zzre;

public sealed class WorldAsset : Asset
{
    public readonly record struct Info(FilePath FullPath);

    public static void Register() =>
        AssetInfoRegistry<Info>.Register<WorldAsset>(AssetLocality.Global);

    private readonly Info info;
    private WorldMesh? mesh;

    public WorldMesh Mesh => mesh ??
        throw new InvalidOperationException("Asset was not yet loaded");

    public WorldAsset(IAssetRegistry registry, Guid assetId, Info info) : base(registry, assetId)
    {
        this.info = info;
    }

    protected override ValueTask<IEnumerable<AssetHandle>> Load()
    {
        mesh = new WorldMesh(diContainer, info.FullPath);
        return NoSecondaryAssets;
    }

    protected override void Unload()
    {
        mesh?.Dispose();
        mesh = null;
    }

    public override string ToString() => $"World {info.FullPath.Parts[^1]}";
}

public static unsafe partial class AssetExtensions
{
    public static AssetHandle<WorldAsset> LoadWorld(this IAssetRegistry registry,
        FilePath path,
        AssetLoadPriority priority) =>
        registry.Load(new WorldAsset.Info(path), priority).As<WorldAsset>();
}
