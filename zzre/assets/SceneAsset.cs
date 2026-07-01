using System;
using System.Threading;
using System.Threading.Tasks;
using zzio;
using zzio.vfs;
using zzio.scn;

namespace zzre;

public sealed class SceneAsset : IAsset<SceneAsset.Info>
{
    private static readonly FilePath BasePath = new("resources/worlds");

    public readonly record struct Info(FilePath FullPath)
    {
        public Info(string name) : this(BasePath.Combine(
            name.EndsWith(".scn", StringComparison.OrdinalIgnoreCase) ? name : name + ".scn"))
        { }

        public Info(SceneType sceneType, int id) : this($"{TypeToPrefix(sceneType)}_{id:D4}.scn")
        { }
    }

    public IAssetRegistry Registry { get; }
    public Scene Scene { get; }
    public IResource Resource { get; } // used for the "Open scene" debug button

    private SceneAsset(IAssetRegistry registry, IResource resource, Scene scene)
    {
        Registry = registry;
        Scene = scene;
        Resource = resource;
    }

    static Task<AssetLoadResult<Info>> IAsset<Info>.LoadAsync(IAssetRegistry registry, Guid assetId, Info info, CancellationToken ct)
    {
        var resourcePool = registry.DIContainer.GetTag<IResourcePool>();
        var resource = resourcePool.FindFile(info.FullPath) ??
            throw new System.IO.FileNotFoundException($"Could not find scene: {info.FullPath}");
        using var stream = resource.OpenContent() ??
            throw new System.IO.FileNotFoundException($"Could not open scene: {info.FullPath}");
        var scene = new Scene();
        scene.Read(stream);
        return Task.FromResult(new AssetLoadResult<Info>(new SceneAsset(registry, resource, scene)));
    }

    public void Dispose() { }

    public override string ToString() => $"Scene {TypeToPrefix(Scene.dataset.sceneType)}_{Scene.dataset.sceneId:D4}";

    private static string TypeToPrefix(SceneType type) => type switch
    {
        SceneType.Overworld => "sc",
        SceneType.Arena => "sd",
        SceneType.MultiplayerArena => "md",
        _ => throw new NotImplementedException($"Unimplemented scene type {type}")
    };
}

static partial class AssetExtensions
{
    public static AssetHandle<SceneAsset> LoadScene(this IAssetRegistry registry, FilePath fullPath, AssetPriority priority) =>
        registry.Load<SceneAsset.Info, SceneAsset>(new(fullPath), priority);

    public static AssetHandle<SceneAsset> LoadScene(this IAssetRegistry registry, string name, AssetPriority priority) =>
        registry.Load<SceneAsset.Info, SceneAsset>(new(name), priority);

    public static AssetHandle<SceneAsset> LoadScene(this IAssetRegistry registry, SceneType type, int id, AssetPriority priority) =>
        registry.Load<SceneAsset.Info, SceneAsset>(new(type, id), priority);
}
