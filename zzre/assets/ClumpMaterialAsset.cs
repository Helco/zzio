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
        MaterialVariant Variant,
        StandardTextureKind? TexturePlaceholder = null);

    public readonly record struct MaterialVariant(
        ModelMaterial.BlendMode BlendMode = ModelMaterial.BlendMode.Opaque,
        bool DepthWrite = true,
        bool DepthTest = true,
        bool HasEnvMap = false,
        bool HasTexShift = true,
        bool HasFog = true)
    {
        public MaterialVariant(zzio.effect.EffectPartRenderMode renderMode, bool depthTest)
            : this(BlendFromRenderMode(renderMode), DepthWrite: false, depthTest, HasTexShift: false) { }

        private static ModelMaterial.BlendMode BlendFromRenderMode(zzio.effect.EffectPartRenderMode renderMode) => renderMode switch
        {
            zzio.effect.EffectPartRenderMode.Additive => ModelMaterial.BlendMode.Additive,
            zzio.effect.EffectPartRenderMode.AdditiveAlpha => ModelMaterial.BlendMode.AdditiveAlpha,
            zzio.effect.EffectPartRenderMode.NormalBlend => ModelMaterial.BlendMode.Alpha,
            _ => throw new NotSupportedException($"Unsupported effect part render mode: {renderMode}")
        };

        public override string ToString() =>
            $"{BlendMode} {Flag(!DepthWrite, "NoZWrite")} {Flag(!DepthTest, "NoZTest")} {Flag(HasEnvMap, "EnvMap")} {Flag(HasTexShift, "TexShift")} {Flag(!HasFog, "NoFog")}";

        private static string Flag(bool enable, string value) => enable ? value : "";
    }

    private AssetHandle<TextureAsset> textureHandle;
    private AssetHandle<SamplerAsset> samplerHandle;

    public IAssetRegistry Registry { get; } = registry;
    public ModelMaterial Material { get; private set; } = null!;

    static async Task<AssetLoadResult<Info>> IAsset<Info>.LoadAsync(IAssetRegistry registry, Info info, CancellationToken ct)
    {
        var diContainer = registry.DIContainer;
        var material = new ModelMaterial(diContainer)
        {
            DebugName = $"ClumpMat {info.TextureName} {info.Variant}"
        };
        SetMaterialVariant(diContainer, material, info.Variant);

        var camera = diContainer.GetTag<Camera>();
        material.Projection.BufferRange = camera.ProjectionRange;
        material.View.BufferRange = camera.ViewRange;
        var samplerHandle = registry.LoadSampler(info.Sampler, AssetPriority.High);
        material.Sampler.Sampler = (await samplerHandle.GetAsync(ct)).Sampler;
        var (initalTexture, textureHandle) = await LoadTexture(registry, info.TextureName, info.TexturePlaceholder, ct);
        material.Texture.Texture = initialTexture;

        return new(new ClumpMaterialAsset(registry)
        {
            samplerHandle = samplerHandle,
            textureHandle = textureHandle,
            Material = material
        });
    }

    private static async Task<(Texture, AssetHandle<TextureAsset>)> LoadTexture(
        IAssetRegistry registry,
        ,
        string? textureName,
        StandardTextureKind? placeholder,
        CancellationToken ct)
    {
        var standardTextures = registry.DIContainer.GetTag<StandardTextures>();
        if (textureName is null && placeholder is null)
            throw new ArgumentNullException(nameof(textureName), "Both textureName and placeholder are null");
        else if (textureName is null)
        {
            return (standardTextures.ByKind(placeholder!.Value), default);
        }
        else if (placeholder is null)
        {
            var handle = registry.LoadTexture(ClumpTextureBasePaths, textureName, AssetPriority.High);
            var texture = await handle.GetAsync(ct);
            return (texture.Texture, handle);
        }
        else
        {
            var handle = registry.LoadTexture(ClumpTextureBasePaths, textureName, AssetPriority.High);
            registry.Apply(handle, h => )
            return (standardTextures.ByKind(placeholder.Value), handle);
        }
    }

    private static void SetMaterialVariant(
        ITagContainer diContainer,
        ModelMaterial material,
        MaterialVariant materialVariant)
    {
        material.IsInstanced = true;
        material.IsSkinned = false;
        material.Blend = materialVariant.BlendMode;
        material.DepthWrite = materialVariant.DepthWrite;
        material.DepthTest = materialVariant.DepthTest;
        material.HasEnvMap = materialVariant.HasEnvMap;
        material.HasTexShift = materialVariant.HasTexShift;
        material.HasFog = materialVariant.HasFog;

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
