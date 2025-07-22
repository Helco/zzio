using System;
using System.Threading;
using System.Threading.Tasks;
using Veldrid;
using zzio;
using zzre.materials;
using zzre.rendering;
using static zzre.ClumpMaterialAsset;

namespace zzre;

public sealed class ClumpMaterialAsset(IAssetRegistry registry) : IAsset<Info>
{
    private static readonly FilePath[] ClumpTextureBasePaths =
    [
        new FilePath("resources/textures/models"),
        new FilePath("resources/textures/worlds"),
        new FilePath("resources/textures/backdrops")
    ];

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

    static async Task<AssetLoadResult<Info>> IAsset<Info>.LoadAsync(IAssetRegistry registry, Guid assetId, Info info, CancellationToken ct)
    {
        var diContainer = registry.DIContainer;
        var material = new ModelMaterial(diContainer)
        {
            DebugName = $"ClumpMat {info.TextureName} {info.Variant}"
        };
        material.Apply(info.Variant, diContainer);

        var camera = diContainer.GetTag<Camera>();
        material.Projection.BufferRange = camera.ProjectionRange;
        material.View.BufferRange = camera.ViewRange;
        var samplerHandle = registry.LoadSampler(info.Sampler, AssetPriority.High);
        material.Sampler.Sampler = (await samplerHandle.GetAsync(ct)).Sampler;
        var (initialTexture, textureHandle) = await AssetExtensions.LoadTextureForMaterial(
            registry, ClumpTextureBasePaths, assetId, info.TextureName, info.TexturePlaceholder, ct);
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
}
