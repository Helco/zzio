using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Veldrid;
using zzio;
using zzio.rwbs;
using zzre.materials;
using zzre.rendering;
using static zzre.ClumpMaterialAsset;

namespace zzre;

public sealed class ClumpMaterialAsset : Asset
{
    private static readonly FilePath[] TextureBasePaths =
    [
        new FilePath("resources/textures/models"),
        new FilePath("resources/textures/worlds"),
        new FilePath("resources/textures/backdrops"),
        new FilePath("resources/textures/actorsex"),
        new FilePath("resources/textures/effects"),
        new FilePath("resources/textures/misc")
    ];

    private const string UseStandardTexture = "marker"; // Funatics never gave us this texture :(

    public readonly record struct Info(
        string? textureName,
        SamplerDescription sampler,
        MaterialVariant config,
        StandardTextureKind? texturePlaceholder = null);

    public readonly record struct MaterialVariant(
        ModelMaterial.BlendMode BlendMode = ModelMaterial.BlendMode.Opaque,
        bool IsInstanced = true,
        bool IsSkinned = false,
        bool DepthWrite = true,
        bool DepthTest = true,
        bool HasEnvMap = false,
        bool HasTexShift = true,
        bool HasFog = true);

    public static void Register() =>
        AssetInfoRegistry<Info>.RegisterLocal<ClumpMaterialAsset>();

    private readonly Info info;
    private ModelMaterial? material;

    public ModelMaterial Material => material ??
        throw new InvalidOperationException("Asset was not yet loaded");

    public ClumpMaterialAsset(IAssetRegistry registry, Guid assetId, Info info) : base(registry, assetId)
    {
        if (info.textureName is null && info.texturePlaceholder is null)
            throw new ArgumentException("ClumpMaterialAsset cannot be loaded without a texture name and placeholder");
        this.info = info;
    }

    protected override bool NeedsSecondaryAssets => false;

    protected override ValueTask<IEnumerable<AssetHandle>> Load()
    {
        material = new ModelMaterial(diContainer)
        {
            IsInstanced = info.config.IsInstanced,
            IsSkinned = info.config.IsSkinned,
            Blend = info.config.BlendMode,
            DepthWrite = info.config.DepthWrite,
            DepthTest = info.config.DepthTest,
            HasEnvMap = info.config.HasEnvMap,
            HasTexShift = info.config.HasTexShift,
            HasFog = info.config.HasFog,
        };

        var camera = diContainer.GetTag<Camera>();
        var standardTextures = diContainer.GetTag<StandardTextures>();
        var samplerHandle = Registry.LoadSampler(info.sampler);
        AssetHandle? textureHandle;
        if (info.textureName == UseStandardTexture) 
        {
            textureHandle = null;
            material.Texture.Texture = standardTextures.ByKind(info.texturePlaceholder ?? StandardTextureKind.White);
        }
        else if (info.texturePlaceholder == null)
        {
            textureHandle = Registry.LoadTexture(TextureBasePaths, info.textureName!, AssetLoadPriority.Synchronous);
            material.Texture.Texture = textureHandle.Value.Get<TextureAsset>().Texture;
        }
        else
        {
            material.Texture.Texture = standardTextures.ByKind(info.texturePlaceholder.Value);
            textureHandle = info.textureName is null ? null
                : Registry.LoadTexture(TextureBasePaths, info.textureName, AssetLoadPriority.High, material);
        }
        material.Sampler.Sampler = samplerHandle.Get().Sampler;
        material.Projection.BufferRange = camera.ProjectionRange;
        material.View.BufferRange = camera.ViewRange;
        material.Factors.Ref = new()
        {
            textureFactor = 1f,
            vertexColorFactor = 1f,
            tintFactor = 1f,
            alphaReference = 0.082352944f
        };
        if (info.config.HasFog && diContainer.TryGetTag<UniformBuffer<FogParams>>(out var fogParams))
            material.FogParams.Buffer = fogParams.Buffer;

        return textureHandle is null
            ? ValueTask.FromResult<IEnumerable<AssetHandle>>([ samplerHandle ])
            : ValueTask.FromResult<IEnumerable<AssetHandle>>([ samplerHandle, textureHandle.Value ]);
    }

    protected override void Unload()
    {
        material?.Dispose();
        material = null;
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

    private static SamplerDescription GetSamplerDescription(RWTexture? rwTexture)
    {
        if (rwTexture is null)
            return SamplerDescription.Point;
        var addressModeU = ConvertAddressMode(rwTexture.uAddressingMode);
        return new()
        {
            AddressModeU = addressModeU,
            AddressModeV = ConvertAddressMode(rwTexture.vAddressingMode, addressModeU),
            Filter = ConvertFilterMode(rwTexture.filterMode),
            MinimumLod = 0,
            MaximumLod = 1000 // this should be VK_LOD_CLAMP_NONE
        };
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
