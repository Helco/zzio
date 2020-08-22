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
            var fullPath = basePath.Combine(nameSection.value);

            var texture =
                TryLoadDDSTexture(fullPath) ??
                TryLoadBMPTexture(fullPath) ??
                null;
            if (texture == null)
                throw new InvalidDataException($"Could not load texture at {fullPath.ToPOSIXString()}");

            texture.Name = nameSection.value;
            return (texture, device.ResourceFactory.CreateSampler(samplerDescription));
        }

        private Texture? TryLoadBMPTexture(FilePath filePath)
        {
            using var textureStream = resourcePool.FindAndOpen(filePath.ToPOSIXString() + ".bmp");
            if (textureStream == null)
                return null;
            try
            {
                return new Veldrid.ImageSharp.ImageSharpTexture(textureStream, true)
                    .CreateDeviceTexture(device, device.ResourceFactory);
            }
            catch(Exception)
            {
                return null;
            }
        }

        private Texture? TryLoadDDSTexture(FilePath filePath)
        {
            using var textureStream = resourcePool.FindAndOpen(filePath.ToPOSIXString() + ".dds");
            if (textureStream == null)
                return null;

            Pfim.IImage? image = null;
            try
            {
                image = Pfim.Dds.Create(textureStream, new Pfim.PfimConfig());
            }
            catch(Exception)
            {
                return null;
            }

            var textureFormat = TryConvertPixelFormat(image.Format);
            if (textureFormat == null)
                return null; // TODO: Support converting Pfim image formats to RGBA32

            var texture = device.ResourceFactory.CreateTexture(TextureDescription.Texture2D(
                width: (uint)image.Width,
                height: (uint)image.Height,
                mipLevels: (uint)image.MipMaps.Length + 1,
                arrayLayers: 1,
                textureFormat.Value,
                TextureUsage.Sampled));
            unsafe
            {
                fixed(void* dataBytePtr = image.Data)
                {
                    IntPtr dataPtr = new IntPtr(dataBytePtr);
                    device.UpdateTexture(texture,
                        source: dataPtr,
                        sizeInBytes: (uint)image.DataLen,
                        x: 0, y: 0, z: 0,
                        width: texture.Width,
                        height: texture.Height,
                        depth: 1,
                        mipLevel: 0,
                        arrayLayer: 0);

                    foreach (var (mipMap, level) in image.MipMaps.Indexed())
                        device.UpdateTexture(texture,
                            source: dataPtr + mipMap.DataOffset,
                            sizeInBytes: (uint)mipMap.DataLen,
                            x: 0, y: 0, z: 0,
                            width: (uint)mipMap.Width,
                            height: (uint)mipMap.Height,
                            depth: 1,
                            mipLevel: (uint)level + 1,
                            arrayLayer: 0);
                }
            }
            return texture;
        }

        private PixelFormat? TryConvertPixelFormat(Pfim.ImageFormat img) => img switch
        {
            Pfim.ImageFormat.Rgb8 => PixelFormat.R8_UNorm,
            Pfim.ImageFormat.Rgba32 => PixelFormat.B8_G8_R8_A8_UNorm,
            _ => null
        };

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
