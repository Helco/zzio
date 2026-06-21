using System;
using System.Threading.Tasks;
using Veldrid;
using zzio;
using zzio.effect;
using zzre.materials;
using zzre.rendering;
using static zzre.materials.EffectMaterial;
using static zzre.EffectMaterialAsset;
using System.Threading;

namespace zzre;

public sealed class EffectMaterialAsset(IAssetRegistry registry) : IAsset<Info>, ITexturedMaterialAsset
{
    private static readonly FilePath[] EffectTextureBasePaths =
    [
        new("resources/textures/effects"),
        new("resources/textures/models")
    ];

    static AssetLocality IAsset.Locality => AssetLocality.Local;
    static bool IAsset.NeedsMainThreadDisposal => true; // an Apply action wants to access Material

    public readonly record struct Info(
        string TextureName,
        BillboardMode BillboardMode,
        BlendMode BlendMode,
        bool DepthTest,
        bool HasFog = true,
        float AlphaReference = 0.03f,
        StandardTextureKind TexturePlaceholder = StandardTextureKind.Clear);

    private AssetHandle<TextureAsset> textureHandle;
    private AssetHandle<SamplerAsset> samplerHandle;

    public IAssetRegistry Registry { get; } = registry;
    public EffectMaterial Material { get; private set; } = null!;
    ITexturedMaterial ITexturedMaterialAsset.Material => Material;

    static async Task<AssetLoadResult<Info>> IAsset<Info>.LoadAsync(IAssetRegistry registry, Guid assetId, Info info, CancellationToken ct)
    {
        var diContainer = registry.DIContainer;
        diContainer.TryGetTag(out UniformBuffer<FogParams> fogParams);
        var material = new EffectMaterial(diContainer)
        {
            DebugName = $"EffectMat {info.TextureName} {info.BillboardMode} {info.BlendMode} {(info.DepthTest ? "" : "NoDepthtest")}",
            DepthTest = info.DepthTest,
            Billboard = info.BillboardMode,
            Blend = info.BlendMode,
            HasFog = info.HasFog && fogParams != null
        };
        material.Factors.Ref = new()
        {
            alphaReference = info.AlphaReference
        };

        var camera = diContainer.GetTag<Camera>();
        material.Projection.BufferRange = camera.ProjectionRange;
        material.View.BufferRange = camera.ViewRange;
        var samplerHandle = registry.LoadSampler(SamplerDescription.Linear, AssetPriority.High);
        material.Sampler.Sampler = (await samplerHandle.GetAsync(ct)).Sampler;
        var (initialTexture, textureHandle) = await AssetExtensions.LoadTextureForMaterial(
            registry,
            EffectTextureBasePaths,
            assetId,
            info.TextureName,
            info.TexturePlaceholder,
            AssetPriority.Low,
            ct);
        material.Texture.Texture = initialTexture;

        return new(new EffectMaterialAsset(registry)
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
    public static AssetHandle<EffectMaterialAsset> LoadEffectMaterial(this IAssetRegistry registry,
        string textureName,
        BillboardMode billboardMode,
        EffectPartRenderMode renderMode,
        bool depthTest,
        AssetPriority priority = AssetPriority.Synchronous) =>
        registry.LoadEffectMaterial(
            textureName,
            billboardMode,
            RenderToBlendMode(renderMode),
            depthTest,
            priority: priority);

    public static unsafe AssetHandle<EffectMaterialAsset> LoadEffectMaterial(this IAssetRegistry registry,
        string TextureName,
        BillboardMode BillboardMode,
        BlendMode BlendMode,
        bool DepthTest,
        bool HasFog = true,
        float AlphaReference = 0.03f,
        StandardTextureKind TexturePlaceholder = StandardTextureKind.Clear,
        AssetPriority priority = AssetPriority.Synchronous) =>
        registry.LoadEffectMaterial(new Info(
            TextureName,
            BillboardMode,
            BlendMode,
            DepthTest,
            HasFog,
            AlphaReference,
            TexturePlaceholder),
            priority);

    public static unsafe AssetHandle<EffectMaterialAsset> LoadEffectMaterial(this IAssetRegistry registry,
        in Info info,
        AssetPriority priority = AssetPriority.Synchronous) =>
        registry.Load<Info, EffectMaterialAsset>(info, priority);

    private static BlendMode RenderToBlendMode(EffectPartRenderMode renderMode) => renderMode switch
    {
        EffectPartRenderMode.Additive => BlendMode.Additive,
        EffectPartRenderMode.AdditiveAlpha => BlendMode.AdditiveAlpha,
        EffectPartRenderMode.NormalBlend => BlendMode.Alpha,
        _ => throw new NotSupportedException($"Unsupported effect part render mode: {renderMode}")
    };
}
