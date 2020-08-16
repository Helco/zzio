using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Veldrid;
using zzio.rwbs;
using zzio.utils;
using zzio.vfs;

namespace zzre
{
    public class TextureLoader
    {
        private readonly IResourcePool resourcePool;
        private readonly GraphicsDevice device;

        public TextureLoader(ITagContainer diContainer)
        {
            resourcePool = diContainer.GetTag<IResourcePool>();
            device = diContainer.GetTag<GraphicsDevice>();
        }

        public FilePath GetTexturePathFromModel(FilePath modelPath)
        {
            var modelDirPartI = modelPath.Parts.IndexOf(p => p.ToLowerInvariant() == "models");
            var context = modelPath.Parts[modelDirPartI + 1];
            return new FilePath("resources/textures").Combine(context);
        }

        public (Texture, Sampler) LoadTexture(FilePath basePath, RWMaterial material)
        {
            var texSection = material.FindChildById(SectionId.Texture, true) as RWTexture;
            if (texSection == null)
                throw new InvalidOperationException($"Given material is not textured");
            return LoadTexture(basePath, texSection);
        }

        public (Texture, Sampler) LoadTexture(FilePath basePath, RWTexture texSection)
        {
            var addressModeU = ConvertAddressMode(texSection.uAddressingMode);
            var samplerDescription = new SamplerDescription()
            {
                AddressModeU = addressModeU,
                AddressModeV = ConvertAddressMode(texSection.vAddressingMode, addressModeU),
                Filter = ConvertFilterMode(texSection.filterMode)
            };

            var nameSection = texSection.FindChildById(SectionId.String, true) as RWString;
            if (nameSection == null)
                throw new InvalidDataException("Could not find filename section in RWTexture");
            using var textureStream = resourcePool.FindAndOpen(basePath.Combine(nameSection.value + ".bmp").ToPOSIXString());
            var texture = new Veldrid.ImageSharp.ImageSharpTexture(textureStream, false);
            var result = (
                texture.CreateDeviceTexture(device, device.ResourceFactory),
                device.ResourceFactory.CreateSampler(samplerDescription));
            result.Item1.Name = nameSection.value;
            return result;
        }

        private SamplerAddressMode ConvertAddressMode(TextureAddressingMode mode, SamplerAddressMode? altMode = null) => mode switch
        {
            TextureAddressingMode.Wrap => SamplerAddressMode.Wrap,
            TextureAddressingMode.Mirror => SamplerAddressMode.Mirror,
            TextureAddressingMode.Clamp => SamplerAddressMode.Clamp,
            TextureAddressingMode.Border => SamplerAddressMode.Border,

            TextureAddressingMode.NATextureAddress => altMode ?? throw new NotImplementedException(),
            TextureAddressingMode.Unknown => throw new NotImplementedException(),
            _ => throw new NotImplementedException(),
        };

        private SamplerFilter ConvertFilterMode(TextureFilterMode mode) => mode switch
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
