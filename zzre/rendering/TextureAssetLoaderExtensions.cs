using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using StbImageSharp;
using Veldrid;
using zzio;
using zzio.rwbs;
using zzio.vfs;

namespace zzre.rendering;

public static class TextureAssetLoaderExtensions
{
    // yeeess funatics, just don't provide the textures you use
    private static readonly IReadOnlySet<string> ExceptionTextures = new HashSet<string>()
    {
        "marker"
    };
    private static readonly IReadOnlyList<string> TextureExtensions = [".dds", ".bmp"];
    private static readonly uint[] WhitePixel = [0xFFFFFFFF];

    public static int Count(this ColorComponents c) => c switch
    {
        ColorComponents.Grey => 1,
        ColorComponents.GreyAlpha => 2,
        ColorComponents.RedGreenBlue => 3,
        ColorComponents.RedGreenBlueAlpha => 4,
        _ => throw new NotImplementedException($"Unimplemented StbImageSharp.ColorComponents: {c}")
    };

    public static Texture ToTexture(this ImageResult image, GraphicsDevice gd, bool srgb = false)
    {
        if (image.BitsPerChannel != 8)
            throw new NotSupportedException($"Unsupported bits per channel: {image.BitsPerChannel}");
        var format = image.ColorComponents switch
        {
            ColorComponents.Grey => PixelFormat.R8_UNorm,
            ColorComponents.GreyAlpha => PixelFormat.R8_G8_UNorm,
            ColorComponents.RedGreenBlue => throw new NotSupportedException("Veldrid does not support 24 bit textures"),
            ColorComponents.RedGreenBlueAlpha when srgb => PixelFormat.R8_G8_B8_A8_UNorm_SRgb,
            ColorComponents.RedGreenBlueAlpha when !srgb => PixelFormat.R8_G8_B8_A8_UNorm,
            _ => throw new NotSupportedException($"Unsupported StbImageSharp.ColorComponents: {image.ColorComponents}")
        };
        var texture = gd.ResourceFactory.CreateTexture(new(
            (uint)image.Width, (uint)image.Height, depth: 1, mipLevels: 1, arrayLayers: 1,
            format, TextureUsage.Sampled, TextureType.Texture2D));
        gd.UpdateTexture(texture, image.Data, 0, 0, 0, (uint)image.Width, (uint)image.Height, depth: 1, mipLevel: 0, arrayLayer: 0);
        return texture;
    }

    public static FilePath GetTexturePathFromModel(this IAssetLoader<Texture> _, FilePath modelPath)
    {
        var modelDirPartI = modelPath.Parts.IndexOf(p => p.Equals("models", StringComparison.OrdinalIgnoreCase));
        var context = modelPath.Parts[modelDirPartI + 1];
        return new FilePath("resources/textures").Combine(context);
    }

    public static (Texture, Sampler) LoadTexture(this IAssetLoader<Texture> loader, FilePath basePath, RWMaterial material) =>
        loader.LoadTexture(new[] { basePath }, material);

    public static (Texture, Sampler) LoadTexture(this IAssetLoader<Texture> loader, IEnumerable<FilePath> basePaths, RWMaterial material)
    {
        if (material.FindChildById(SectionId.Texture, true) is not RWTexture texSection)
            throw new InvalidOperationException("Given material is not textured");
        return loader.LoadTexture(basePaths, texSection);
    }

    public static (Texture, Sampler) LoadTexture(this IAssetLoader<Texture> loader, FilePath basePath, RWTexture texSection) =>
        loader.LoadTexture(new[] { basePath }, texSection);

    public static (Texture, Sampler) LoadTexture(this IAssetLoader<Texture> loader, IEnumerable<FilePath> basePaths, RWTexture texSection)
    {
        var device = loader.DIContainer.GetTag<GraphicsDevice>();

        var addressModeU = ConvertAddressMode(texSection.uAddressingMode);
        var samplerDescription = new SamplerDescription()
        {
            AddressModeU = addressModeU,
            AddressModeV = ConvertAddressMode(texSection.vAddressingMode, addressModeU),
            Filter = ConvertFilterMode(texSection.filterMode),
            MaximumLod = 1000
        };
        var sampler = device.ResourceFactory.CreateSampler(samplerDescription);

        if (texSection.FindChildById(SectionId.String, true) is not RWString nameSection)
            throw new InvalidDataException("Could not find filename section in RWTexture");

        var texture = loader.LoadTexture(basePaths, nameSection.value);
        return (texture, sampler);
    }

    public static Texture LoadTexture(this IAssetLoader<Texture> loader, FilePath basePath, string texName) =>
        LoadTexture(loader, new[] { basePath }, texName);

    public static Texture LoadTexture(this IAssetLoader<Texture> loader, IEnumerable<FilePath> basePaths, string texName)
    {
        if (ExceptionTextures.Contains(texName))
            return loader.CreateExceptionTexture(texName);

        var resourcePool = loader.DIContainer.GetTag<IResourcePool>();
        var texture = basePaths
            .Select(basePath => basePath.Combine(texName))
            .SelectMany(fullPath => TextureExtensions.Select(ext => fullPath.ToPOSIXString() + ext))
            .Select(resourcePool.FindFile)
            .Where(res => res != null)
            .Select(res => loader.TryLoad(res!, out var texture) ? texture : null)
            .FirstOrDefault(tex => tex != null) ?? throw new InvalidDataException($"Could not load texture {texName}");
        texture.Name = texName;
        return texture;
    }

    private static Texture CreateExceptionTexture(this IAssetLoader<Texture> loader, string name)
    {
        var device = loader.DIContainer.GetTag<GraphicsDevice>();
        var texture = device.ResourceFactory.CreateTexture(
            new TextureDescription(1, 1, 1, 1, 1, PixelFormat.R8_G8_B8_A8_UNorm, TextureUsage.Sampled, TextureType.Texture2D, TextureSampleCount.Count1));
        device.UpdateTexture(texture, WhitePixel, 0, 0, 0, 1, 1, 1, 0, 0);
        texture.Name = $"{name} (white)";
        return texture;
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
