using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Veldrid;
using zzio;
using zzio.rwbs;
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

    public static void Register() =>
        AssetInfoRegistry<Info>.Register<ClumpAsset>();

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
        return NoSecondaryAssets;
    }

    protected override void Unload()
    {
        mesh?.Dispose();
        mesh = null;
    }
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

    public static AssetHandle<ClumpAsset> LoadActorClump(this IAssetRegistry registry,
        DefaultEcs.Entity entity,
        string modelName,
        AssetLoadPriority priority,
        ClumpMaterialAsset.MaterialVariant? variant = null,
        StandardTextureKind? texturePlaceholder = null) =>
        registry.LoadClump(entity, ClumpAsset.Info.Actor(modelName), priority, variant, texturePlaceholder);

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
        var asset = handle.Get<ClumpAsset>();
        entity.Set(asset.Mesh);
    }

    private static void ApplyClumpToEntityWithMaterials(AssetHandle handle,
        ref readonly (IAssetRegistry, DefaultEcs.Entity, ClumpMaterialAsset.MaterialVariant, StandardTextureKind?) context)
    {
        var (registry, entity, materialConfig, placeholder) = context;
        var clumpMesh = handle.Get<ClumpAsset>().Mesh;
        entity.Set(clumpMesh);

        var materials = new List<materials.ModelMaterial>(clumpMesh.Materials.Count);
        var handles = new AssetHandle[clumpMesh.Materials.Count];
        for (int i = 0; i < handles.Length; i++)
        {
            var rwMaterial = clumpMesh.Materials[i];
            var rwTexture = (RWTexture)rwMaterial.FindChildById(SectionId.Texture, true)!;
            var rwTextureName = (RWString)rwTexture.FindChildById(SectionId.String, true)!;
            var addressModeU = ConvertAddressMode(rwTexture.uAddressingMode);
            var samplerDescription = new SamplerDescription()
            {
                AddressModeU = addressModeU,
                AddressModeV = ConvertAddressMode(rwTexture.vAddressingMode, addressModeU),
                Filter = ConvertFilterMode(rwTexture.filterMode),
                MinimumLod = 0,
                MaximumLod = 1000 // this should be VK_LOD_CLAMP_NONE
            };

            handles[i] = registry.Load(
                new ClumpMaterialAsset.Info(rwTextureName.value, samplerDescription, materialConfig, placeholder),
                AssetLoadPriority.Synchronous);
            materials.Add(handles[i].Get<ClumpMaterialAsset>().Material);
        }
        entity.Set(handles);
        entity.Set(materials);
    }

    private static SamplerAddressMode ConvertAddressMode(TextureAddressingMode mode, SamplerAddressMode? altMode = null) => mode switch
    {
        TextureAddressingMode.Wrap => SamplerAddressMode.Wrap,
        TextureAddressingMode.Mirror => SamplerAddressMode.Mirror,
        TextureAddressingMode.Clamp => SamplerAddressMode.Clamp,
        TextureAddressingMode.Border => SamplerAddressMode.Border,

        TextureAddressingMode.NATextureAddress => altMode ?? throw new NotImplementedException(),
        TextureAddressingMode.Unknown => throw new NotImplementedException(),
        _ => throw new NotImplementedException(),
    };


    private static SamplerFilter ConvertFilterMode(TextureFilterMode mode) => mode switch
    {
        TextureFilterMode.Nearest => SamplerFilter.MinPoint_MagPoint_MipPoint,
        TextureFilterMode.Linear => SamplerFilter.MinLinear_MagLinear_MipPoint,
        TextureFilterMode.MipNearest => SamplerFilter.MinPoint_MagPoint_MipPoint,
        TextureFilterMode.MipLinear => SamplerFilter.MinLinear_MagLinear_MipPoint,
        TextureFilterMode.LinearMipNearest => SamplerFilter.MinPoint_MagPoint_MipLinear,
        TextureFilterMode.LinearMipLinear => SamplerFilter.MinLinear_MagLinear_MipLinear,

        TextureFilterMode.NAFilterMode => throw new NotImplementedException(),
        TextureFilterMode.Unknown => throw new NotImplementedException(),
        _ => throw new NotImplementedException(),
    };
}
