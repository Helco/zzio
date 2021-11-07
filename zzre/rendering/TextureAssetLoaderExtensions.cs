using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Veldrid;
using zzio;
using zzio.rwbs;
using zzio.utils;
using zzio.vfs;

namespace zzre.rendering
{
    public static class TextureAssetLoaderExtensions
    {
        public static FilePath GetTexturePathFromModel(this IAssetLoader<Texture> _, FilePath modelPath)
        {
            var modelDirPartI = modelPath.Parts.IndexOf(p => p.ToLowerInvariant() == "models");
            var context = modelPath.Parts[modelDirPartI + 1];
            return new FilePath("resources/textures").Combine(context);
        }

        public static (Texture, Sampler) LoadTexture(this IAssetLoader<Texture> loader, FilePath basePath, RWMaterial material) =>
            loader.LoadTexture(new[] { basePath }, material);

        public static (Texture, Sampler) LoadTexture(this IAssetLoader<Texture> loader, IEnumerable<FilePath> basePaths, RWMaterial material)
        {
            var texSection = material.FindChildById(SectionId.Texture, true) as RWTexture;
            if (texSection == null)
                throw new InvalidOperationException($"Given material is not textured");
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
                Filter = ConvertFilterMode(texSection.filterMode)
            };
            var sampler = device.ResourceFactory.CreateSampler(samplerDescription);

            var nameSection = texSection.FindChildById(SectionId.String, true) as RWString;
            if (nameSection == null)
                throw new InvalidDataException("Could not find filename section in RWTexture");

            var texture = loader.LoadTexture(basePaths, nameSection.value);
            return (texture, sampler);
        }

        public static Texture LoadTexture(this IAssetLoader<Texture> loader, FilePath basePath, string texName) =>
            LoadTexture(loader, new[] { basePath }, texName);

        public static Texture LoadTexture(this IAssetLoader<Texture> loader, IEnumerable<FilePath> basePaths, string texName)
        {
            var resourcePool = loader.DIContainer.GetTag<IResourcePool>();
            var texture = basePaths
                .Select(basePath => basePath.Combine(texName))
                .SelectMany(fullPath => new[] { ".dds", ".bmp" }.Select(ext => fullPath.ToPOSIXString() + ext))
                .Select(fullPath => resourcePool.FindFile(fullPath))
                .Where(res => res != null)
                .Select(res => loader.TryLoad(res!, out var texture) ? texture : null)
                .FirstOrDefault(tex => tex != null);

            if (texture == null)
                throw new InvalidDataException($"Could not load texture {texName}");
            texture.Name = texName;
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
}
