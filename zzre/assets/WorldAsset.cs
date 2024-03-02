using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using zzio;
using zzre.rendering;

namespace zzre;

public sealed class WorldAsset : Asset
{
    private static readonly FilePath BasePath = new("resources/worlds");

    public readonly record struct Info(string WorldName)
    {
        public FilePath FullPath => BasePath.Combine(
            WorldName.EndsWith(".bsp", StringComparison.OrdinalIgnoreCase) ? WorldName : WorldName + ".bsp");
    }

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

    public override string ToString() => $"World {info.WorldName}";
}

public static unsafe partial class AssetExtensions
{
    public static AssetHandle<WorldAsset> LoadWorld(this IAssetRegistry registry,
        string worldName,
        AssetLoadPriority priority) =>
        registry.Load(new WorldAsset.Info(worldName), priority).As<WorldAsset>();
}
