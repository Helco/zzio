using System;
using System.Collections.Generic;
using Veldrid;
using zzio;
using zzio.rwbs;
using zzre.materials;
using zzre.rendering;
using static zzre.ActorMaterialAsset;

namespace zzre;

public sealed class ActorMaterialAsset : ModelMaterialAsset
{
    private static readonly FilePath[] ActorTextureBasePaths =
    [
        new FilePath("resources/textures/actorsex"),
        new FilePath("resources/textures/models"),
        new FilePath("resources/textures/worlds"),
    ];
    protected override IReadOnlyList<FilePath> TextureBasePaths => ActorTextureBasePaths;

    public readonly record struct Info(
        string? textureName,
        SamplerDescription sampler,
        IColor color,
        bool isSkinned,
        StandardTextureKind? texturePlaceholder = null);

    public static void Register() =>
        AssetInfoRegistry<Info>.RegisterLocal<ActorMaterialAsset>();

    private readonly IColor color;
    private readonly bool isSkinned;

    public ActorMaterialAsset(IAssetRegistry registry, Guid assetId, Info info)
        : base(registry, assetId, info.textureName, info.sampler, info.texturePlaceholder)
    {
        color = info.color;
        isSkinned = info.isSkinned;
    }

    protected override void SetMaterialVariant(ModelMaterial material)
    {
        Material.Blend = ModelMaterial.BlendMode.Opaque;
        Material.IsInstanced = false;
        Material.IsSkinned = isSkinned;
        Material.HasTexShift = false;
        Material.HasFog = true;
        Material.Tint.Ref = color;
        if (diContainer.TryGetTag<UniformBuffer<FogParams>>(out var fogParams))
            Material.FogParams.Buffer = fogParams.Buffer;
    }
}

partial class AssetExtensions
{
    public static AssetHandle<ActorMaterialAsset> LoadActorMaterial(this IAssetRegistry registry,
        RWMaterial rwMaterial, bool isSkinned)
    {
        var rwTexture = rwMaterial.FindChildById(SectionId.Texture, true) as RWTexture;
        var rwTextureName = (rwTexture?.FindChildById(SectionId.String, true) as RWString)?.value;
        var samplerDescription = GetSamplerDescription(rwTexture);
        return registry.Load(
            new Info(rwTextureName, samplerDescription, rwMaterial.color, isSkinned, StandardTextureKind.White),
            AssetLoadPriority.Synchronous)
            .As<ActorMaterialAsset>();
    }
}
