using System;
using System.Collections.Generic;
using Veldrid;
using zzio;
using zzio.rwbs;
using zzre.materials;
using zzre.rendering;

namespace zzre;

public sealed class WorldMaterialAsset : ModelMaterialAsset
{
    private static readonly FilePath[] WorldTextureBasePaths =
    [
        new FilePath("resources/textures/worlds")
    ];
    protected override IReadOnlyList<FilePath> TextureBasePaths => WorldTextureBasePaths;

    public readonly record struct Info(
        string? textureName,
        SamplerDescription sampler);

    public static void Register() =>
        AssetInfoRegistry<Info>.Register<WorldMaterialAsset>(AssetLocality.Context);

    public WorldMaterialAsset(IAssetRegistry registry, Guid assetId, Info info)
        : base(registry, assetId, info.textureName, info.sampler, StandardTextureKind.White)
    {
    }

    protected override void SetMaterialVariant(ModelMaterial material)
    {
        Material.IsInstanced = false;
        Material.IsSkinned = false;
        Material.Blend = ModelMaterial.BlendMode.Opaque;
        Material.HasTexShift = false;
        Material.HasFog = diContainer.TryGetTag<UniformBuffer<FogParams>>(out var fogParams);

        material.Factors.Ref = new()
        {
            textureFactor = 1f,
            vertexColorFactor = 1f,
            tintFactor = 1f,
            alphaReference = 0.6f
        };
        if (fogParams is not null)
            material.FogParams.Buffer = fogParams.Buffer;
    }
}

partial class AssetExtensions
{
    public static AssetHandle<WorldMaterialAsset> LoadWorldMaterial(this IAssetRegistry registry,
        RWMaterial rwMaterial,
        AssetLoadPriority priority)
    {
        var rwTexture = rwMaterial.FindChildById(SectionId.Texture, true) as RWTexture;
        var rwTextureName = (rwTexture?.FindChildById(SectionId.String, true) as RWString)?.value;
        var samplerDescription = GetSamplerDescription(rwTexture);
        return registry.Load(
            new WorldMaterialAsset.Info(rwTextureName, samplerDescription),
            priority)
            .As<WorldMaterialAsset>();
    }
}
