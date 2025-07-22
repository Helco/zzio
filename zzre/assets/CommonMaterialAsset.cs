using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Veldrid;
using zzio;
using zzio.rwbs;
using zzre.rendering;

namespace zzre;

public interface ITexturedMaterialAsset : IAsset
{
    ITexturedMaterial Material { get; }
}

partial class AssetExtensions
{
    internal static async Task<(Texture, AssetHandle<TextureAsset>)> LoadTextureForMaterial(
        IAssetRegistry registry,
        IReadOnlyList<FilePath> texturePaths,
        Guid assetId,
        string? textureName,
        StandardTextureKind? placeholder,
        CancellationToken ct)
    {
        var standardTextures = registry.DIContainer.GetTag<StandardTextures>();
        if (textureName is null && placeholder is null)
            throw new ArgumentNullException(nameof(textureName), "Both textureName and placeholder are null");
        else if (textureName is null or "marker") // Funatics did not gave us this texture :(
        {
            return (standardTextures.ByKind(placeholder!.Value), default);
        }
        else if (placeholder is null)
        {
            var handle = registry.LoadTexture(texturePaths, textureName, AssetPriority.High);
            var texture = await handle.GetAsync(ct);
            return (texture.Texture, handle);
        }
        else
        {
            var handle = registry.LoadTexture(texturePaths, textureName, AssetPriority.High);
            registry.Apply(handle, h =>
            {
                if (registry.TryGet<IAsset>(assetId, out var materialHandle) &&
                    materialHandle.Asset is ITexturedMaterialAsset { Material: ITexturedMaterial material })
                    material.Texture.Texture = h.Asset?.Texture ?? material.Texture.Texture;
            });
            return (standardTextures.ByKind(placeholder.Value), handle);
        }
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
