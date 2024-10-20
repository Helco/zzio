using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using zzio;
using zzre.rendering;

namespace zzre;

public sealed class ClumpAsset : Asset
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

    public static void Register() =>
        AssetInfoRegistry<Info>.Register<ClumpAsset>(AssetLocality.Global);

    private readonly Info info;
    private ClumpMesh? mesh;

    public string Name => info.Name;
    public ClumpMesh Mesh => mesh ??
        throw new InvalidOperationException("Asset was not yet loaded");

    public ClumpAsset(IAssetRegistry registry, Guid assetId, Info info) : base(registry, assetId)
    {
        this.info = info;
    }

    protected override ValueTask<IEnumerable<AssetHandle>> Load()
    {
        mesh = new ClumpMesh(diContainer, info.FullPath);
        return NoSecondaryAssets;
    }

    protected override void Unload()
    {
        mesh?.Dispose();
        mesh = null;
    }

    protected override string ToStringInner() => $"Clump {info.Name} ({info.Directory})";
}

public static unsafe partial class AssetExtensions
{
    public static AssetHandle<ClumpAsset> LoadBackdrop(this IAssetRegistry registry,
        DefaultEcs.Entity entity,
        string modelName,
        AssetLoadPriority priority,
        ClumpMaterialAsset.MaterialVariant? variant = null,
        StandardTextureKind? texturePlaceholder = null) =>
        registry.LoadClump(entity, ClumpAsset.Info.Backdrop(modelName), priority, variant, texturePlaceholder);

    public static AssetHandle<ClumpAsset> LoadModel(this IAssetRegistry registry,
        DefaultEcs.Entity entity,
        string modelName,
        AssetLoadPriority priority,
        ClumpMaterialAsset.MaterialVariant? variant = null,
        StandardTextureKind? texturePlaceholder = null) =>
        registry.LoadClump(entity, ClumpAsset.Info.Model(modelName), priority, variant, texturePlaceholder);

    public static AssetHandle<ClumpAsset> LoadClump(this IAssetRegistry registry,
        DefaultEcs.Entity entity,
        ClumpAsset.Info info,
        AssetLoadPriority priority,
        ClumpMaterialAsset.MaterialVariant? variant = null,
        StandardTextureKind? texturePlaceholder = null)
    {
        var handle = variant.HasValue
            ? registry.Load(info, priority, &ApplyClumpToEntityWithMaterials, (registry, entity, variant.Value, texturePlaceholder))
            : registry.Load(info, priority, &ApplyClumpToEntity, entity);
        entity.Set(handle);
        return handle.As<ClumpAsset>();
    }

    private static void ApplyClumpToEntity(AssetHandle handle, ref readonly DefaultEcs.Entity entity)
    {
        if (!entity.IsAlive)
            return;
        var asset = handle.Get<ClumpAsset>();
        entity.Set(asset.Mesh);
    }

    private static void ApplyClumpToEntityWithMaterials(AssetHandle handle,
        ref readonly (IAssetRegistry, DefaultEcs.Entity, ClumpMaterialAsset.MaterialVariant, StandardTextureKind?) context)
    {
        var (registry, entity, materialConfig, placeholder) = context;
        if (!entity.IsAlive)
            return;
        var clumpMesh = handle.Get<ClumpAsset>().Mesh;
        entity.Set(clumpMesh);

        var materials = new List<materials.ModelMaterial>(clumpMesh.Materials.Count);
        var handles = new AssetHandle[clumpMesh.Materials.Count];
        for (int i = 0; i < handles.Length; i++)
        {
            var materialHandle = registry.LoadClumpMaterial(clumpMesh.Materials[i], materialConfig, placeholder);
            handles[i] = materialHandle;
            materials.Add(materialHandle.Get().Material);
        }
        entity.Set(handles);
        entity.Set(materials);
    }
}
