using System;
using System.Threading;
using System.Threading.Tasks;
using zzio;
using zzre.rendering;

namespace zzre;

public sealed class ClumpAsset : IAsset<ClumpAsset.Info>
{
    private static readonly FilePath BasePath = new("resources/models/");

    public readonly record struct Info(FilePath FullPath)
    {
        public static Info Model(string name) => new("models", name);
        public static Info Actor(string name) => new("actorsex", name);
        public static Info Backdrop(string name) => new("backdrops", name);

        public Info(string directory, string name) : this(BasePath.Combine(directory,
            name.EndsWith(".dff", StringComparison.OrdinalIgnoreCase) ? name : name + ".dff"))
        { }

        public string Directory => FullPath.Parts[^2];
        public string Name => FullPath.Parts[^1];
    }

    private readonly Info info;

    public string Name => info.Name;
    public IAssetRegistry Registry { get; }
    public ClumpMesh Mesh { get; private set; }

    private ClumpAsset(IAssetRegistry registry, Info info, ClumpMesh mesh)
    {
        this.info = info;
        Registry = registry;
        Mesh = mesh;
    }

    public void Dispose()
    {
        Mesh.Dispose();
        Mesh = null!;
    }

    static Task<AssetLoadResult<Info>> IAsset<Info>.LoadAsync(IAssetRegistry registry, Info info, CancellationToken ct)
    {
        var mesh = new ClumpMesh(registry.DIContainer, info.FullPath);
        return Task.FromResult(new AssetLoadResult<Info>(new ClumpAsset(registry, info, mesh)));
    }

    public override string ToString() => $"Clump {info.Name} ({info.Directory})";
}

public static partial class AssetExtensions
{
    public static AssetHandle<ClumpAsset> LoadModelClump(this IAssetRegistry registry, string name, AssetPriority priority) =>
        registry.Load<ClumpAsset.Info, ClumpAsset>(ClumpAsset.Info.Model(name), priority);

    public static AssetHandle<ClumpAsset> LoadActorClump(this IAssetRegistry registry, string name, AssetPriority priority) =>
        registry.Load<ClumpAsset.Info, ClumpAsset>(ClumpAsset.Info.Actor(name), priority);

    public static AssetHandle<ClumpAsset> LoadBackdropClump(this IAssetRegistry registry, string name, AssetPriority priority) =>
        registry.Load<ClumpAsset.Info, ClumpAsset>(ClumpAsset.Info.Backdrop(name), priority);
}
