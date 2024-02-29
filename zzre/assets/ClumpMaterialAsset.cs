using System;
using System.Collections.Generic;
using Veldrid;
using zzio;
using zzio.rwbs;
using zzre.materials;
using zzre.rendering;
using static zzre.ClumpMaterialAsset;

namespace zzre;

public sealed class ClumpMaterialAsset : ModelMaterialAsset
{
    private static readonly FilePath[] ClumpTextureBasePaths =
    [
        new FilePath("resources/textures/models"),
        new FilePath("resources/textures/worlds"),
        new FilePath("resources/textures/backdrops")
    ];
    protected override IReadOnlyList<FilePath> TextureBasePaths => ClumpTextureBasePaths;

    public readonly record struct Info(
        string? textureName,
        SamplerDescription sampler,
        MaterialVariant config,
        StandardTextureKind? texturePlaceholder = null);

    public static void Register() =>
        AssetInfoRegistry<Info>.Register<ClumpMaterialAsset>(AssetLocality.Context);

    public readonly record struct MaterialVariant(
        ModelMaterial.BlendMode BlendMode = ModelMaterial.BlendMode.Opaque,
        bool DepthWrite = true,
        bool DepthTest = true,
        bool HasEnvMap = false,
        bool HasTexShift = true,
        bool HasFog = true);

    private readonly MaterialVariant materialVariant;

    public ClumpMaterialAsset(IAssetRegistry registry, Guid assetId, Info info)
        : base(registry, assetId, info.textureName, info.sampler, info.texturePlaceholder)
    {
        materialVariant = info.config;
    }

    protected override void SetMaterialVariant(ModelMaterial material)
    {
        Material.IsInstanced = true;
        Material.IsSkinned = false;
        Material.Blend = materialVariant.BlendMode;
        Material.DepthWrite = materialVariant.DepthWrite;
        Material.DepthTest = materialVariant.DepthTest;
        Material.HasEnvMap = materialVariant.HasEnvMap;
        Material.HasTexShift = materialVariant.HasTexShift;
        Material.HasFog = materialVariant.HasFog;

        material.Factors.Ref = new()
        {
            textureFactor = 1f,
            vertexColorFactor = 1f,
            tintFactor = 1f,
            alphaReference = 0.082352944f
        };
        if (materialVariant.HasFog && diContainer.TryGetTag<UniformBuffer<FogParams>>(out var fogParams))
            material.FogParams.Buffer = fogParams.Buffer;
    }
}

partial class AssetExtensions
{
    public static AssetHandle<ClumpMaterialAsset> LoadClumpMaterial(this IAssetRegistry registry,
        string? textureName,
        SamplerDescription sampler,
        MaterialVariant config,
        StandardTextureKind? texturePlaceholder = null) =>
        registry.Load(
            new Info(textureName, sampler, config, texturePlaceholder),
            AssetLoadPriority.Synchronous)
        .As<ClumpMaterialAsset>();

    public static AssetHandle<ClumpMaterialAsset> LoadClumpMaterial(this IAssetRegistry registry,
        StandardTextureKind texture,
        MaterialVariant config) =>
        registry.Load(
            new Info(null, SamplerDescription.Point, config, texture),
            AssetLoadPriority.Synchronous)
        .As<ClumpMaterialAsset>();

    public static AssetHandle<ClumpMaterialAsset> LoadClumpMaterial(this IAssetRegistry registry,
        RWMaterial rwMaterial,
        MaterialVariant config,
        StandardTextureKind? texturePlaceholder = null)
    {
        var rwTexture = rwMaterial.FindChildById(SectionId.Texture, true) as RWTexture;
        var rwTextureName = (rwTexture?.FindChildById(SectionId.String, true) as RWString)?.value;
        var samplerDescription = GetSamplerDescription(rwTexture);
        return registry.Load(
            new Info(rwTextureName, samplerDescription, config, texturePlaceholder),
            AssetLoadPriority.Synchronous)
            .As<ClumpMaterialAsset>();
    }
}
