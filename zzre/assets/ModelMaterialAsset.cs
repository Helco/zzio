using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Veldrid;
using zzio;
using zzio.rwbs;
using zzre.materials;
using zzre.rendering;

namespace zzre;

public abstract class ModelMaterialAsset : Asset
{
    private const string UseStandardTexture = "marker"; // Funatics never gave us this texture :(

    private readonly string? textureName;
    private readonly SamplerDescription sampler;
    private readonly StandardTextureKind? texturePlaceholder;
    private ModelMaterial? material;

    public ModelMaterial Material => material ??
        throw new InvalidOperationException("Asset was not yet loaded");

    public ModelMaterialAsset(IAssetRegistry registry, Guid assetId,
        string? textureName,
        SamplerDescription sampler,
        StandardTextureKind? texturePlaceholder)
        : base(registry, assetId)
    {
        if (textureName is null && texturePlaceholder is null)
            throw new ArgumentException("ClumpMaterialAsset cannot be loaded without a texture name and placeholder");
        this.textureName = textureName;
        this.sampler = sampler;
        this.texturePlaceholder = texturePlaceholder;
    }

    protected override bool NeedsSecondaryAssets => false;

    protected override ValueTask<IEnumerable<AssetHandle>> Load()
    {
        material = new ModelMaterial(diContainer);
        SetMaterialVariant(material);

        var camera = diContainer.GetTag<Camera>();
        var samplerHandle = Registry.LoadSampler(sampler);
        var textureHandle = LoadTexture();
        material.Sampler.Sampler = samplerHandle.Get().Sampler;
        material.Projection.BufferRange = camera.ProjectionRange;
        material.View.BufferRange = camera.ViewRange;

        return textureHandle is null
            ? ValueTask.FromResult<IEnumerable<AssetHandle>>([ samplerHandle ])
            : ValueTask.FromResult<IEnumerable<AssetHandle>>([ samplerHandle, textureHandle.Value ]);
    }

    private AssetHandle? LoadTexture()
    {
        if (material is null)
            return null;
        var standardTextures = diContainer.GetTag<StandardTextures>();
        if (textureName == UseStandardTexture)
        {
            material.Texture.Texture = standardTextures.ByKind(texturePlaceholder ?? StandardTextureKind.White);
            return null;
        }
        else if (texturePlaceholder == null)
        {
            var handle = Registry.LoadTexture(TextureBasePaths, textureName!, AssetLoadPriority.Synchronous);
            material.Texture.Texture = handle.Get().Texture;
            return handle;
        }
        else
        {
            material.Texture.Texture = standardTextures.ByKind(texturePlaceholder.Value);
            return textureName is null ? null
                : Registry.LoadTexture(TextureBasePaths, textureName, AssetLoadPriority.High, material);
        }
    }

    protected override void Unload()
    {
        material?.Dispose();
        material = null;
    }

    protected abstract IReadOnlyList<FilePath> TextureBasePaths { get; }
    protected abstract void SetMaterialVariant(ModelMaterial material);
}

partial class AssetExtensions
{
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
