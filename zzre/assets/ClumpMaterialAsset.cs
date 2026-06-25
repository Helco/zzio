using System;
using System.Threading;
using System.Threading.Tasks;
using Veldrid;
using zzio;
using zzio.rwbs;
using zzre.materials;
using zzre.rendering;
using static zzre.ClumpMaterialAsset;

namespace zzre;

public sealed class ClumpMaterialAsset(IAssetRegistry registry) : IAsset<Info>, ITexturedMaterialAsset
{
    private static readonly FilePath[] ClumpTextureBasePaths =
    [
        new FilePath("resources/textures/models"),
        new FilePath("resources/textures/worlds"),
        new FilePath("resources/textures/backdrops")
    ];

    private static readonly ModelFactors ModelFactors = new()
    {
        textureFactor = 1f,
        vertexColorFactor = 1f,
        tintFactor = 1f,
        alphaReference = 0.082352944f
    };

    static AssetLocality IAsset.Locality => AssetLocality.Local;
    static bool IAsset.NeedsMainThreadDisposal => true; // an Apply action wants to access Material

    public readonly record struct Info(
        string? TextureName,
        SamplerDescription Sampler,
        ModelMaterial.Variant Variant,
        StandardTextureKind? TexturePlaceholder = null);

    private AssetHandle<TextureAsset> textureHandle;
    private AssetHandle<SamplerAsset> samplerHandle;

    public IAssetRegistry Registry { get; } = registry;
    public ModelMaterial Material { get; private set; } = null!;
    ITexturedMaterial ITexturedMaterialAsset.Material => Material;

    static async Task<AssetLoadResult<Info>> IAsset<Info>.LoadAsync(IAssetRegistry registry, Guid assetId, Info info, CancellationToken ct)
    {
        var diContainer = registry.DIContainer;
        var material = new ModelMaterial(diContainer)
        {
            DebugName = $"ClumpMat {info.TextureName} {info.Variant}"
        };
        material.Apply(info.Variant, ModelFactors, diContainer);

        var camera = diContainer.GetTag<Camera>();
        material.Projection.BufferRange = camera.ProjectionRange;
        material.View.BufferRange = camera.ViewRange;
        var samplerHandle = registry.LoadSampler(info.Sampler, AssetPriority.High);
        material.Sampler.Sampler = (await samplerHandle.GetAsync(ct)).Sampler;
        var (initialTexture, textureHandle) = await AssetExtensions.LoadTextureForMaterial(
            registry,
            ClumpTextureBasePaths,
            assetId,
            info.TextureName,
            info.TexturePlaceholder,
            AssetPriority.High,
            ct);
        material.Texture.Texture = initialTexture;

        return new(new ClumpMaterialAsset(registry)
        {
            samplerHandle = samplerHandle,
            textureHandle = textureHandle,
            Material = material
        });
    }

    public void Dispose()
    {
        textureHandle.Dispose();
        samplerHandle.Dispose();
        Material?.Dispose();
        Material = null!;
    }

    public override string ToString() => Material?.DebugName ?? "Disposed ClumpMaterial";
}

partial class AssetExtensions
{
    public static AssetHandle<ClumpMaterialAsset> LoadClumpMaterial(this IAssetRegistry registry,
        string? textureName,
        SamplerDescription sampler,
        ModelMaterial.Variant config,
        StandardTextureKind? texturePlaceholder = null,
        AssetPriority priority = AssetPriority.Synchronous) =>
        registry.Load<Info, ClumpMaterialAsset>(
            new(textureName, sampler, config, texturePlaceholder),
            priority
        );

    public static AssetHandle<ClumpMaterialAsset> LoadClumpMaterial(this IAssetRegistry registry,
        RWMaterial rwMaterial,
        ModelMaterial.Variant config,
        StandardTextureKind? texturePlaceholder = null,
        AssetPriority priority = AssetPriority.Synchronous)
    {
        var rwTexture = rwMaterial.FindChildById(SectionId.Texture, true) as RWTexture;
        var rwTextureName = (rwTexture?.FindChildById(SectionId.String, true) as RWString)?.value;
        var samplerDescription = GetSamplerDescription(rwTexture);
        return registry.LoadClumpMaterial(
            rwTextureName,
            samplerDescription,
            config,
            texturePlaceholder,
            priority);
    }
}
