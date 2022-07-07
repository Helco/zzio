using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Veldrid;
using zzio;
using zzio.vfs;
using zzre.rendering;

namespace zzre
{
    public class TextureAssetLoader : IAssetLoader<Texture>
    {
        public ITagContainer DIContainer { get; }
        private readonly IResourcePool resourcePool;
        private readonly GraphicsDevice device;

        public TextureAssetLoader(ITagContainer diContainer)
        {
            DIContainer = diContainer;
            resourcePool = diContainer.GetTag<IResourcePool>();
            device = diContainer.GetTag<GraphicsDevice>();
        }

        public bool TryLoad(IResource resource, [NotNullWhen(true)] out Texture? texture)
        {
            var extension = resource.Path.Extension?.ToLower();
            texture = extension switch
            {
                "dds" => TryLoadDDSTexture(resource),
                "bmp" => TryLoadBMPTexture(resource),
                _ => throw new NotSupportedException($"Unsupported texture resource extension \"{extension}\"")
            };
            return texture != null;
        }

        public void Clear() { }

        private Texture? TryLoadBMPTexture(IResource resource)
        {
            using var textureStream = resource.OpenContent();
            if (textureStream == null)
                return null;
            try
            {
                return new Veldrid.ImageSharp.ImageSharpTexture(textureStream, true)
                    .CreateDeviceTexture(device, device.ResourceFactory);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private Texture? TryLoadDDSTexture(IResource resource)
        {
            Pfim.IImage? TryCreate(Stream stream)
            {
                try
                {
                    return Pfim.Dds.Create(stream, new Pfim.PfimConfig());
                }
                catch (Exception)
                {
                    return null;
                }
            }

            using var textureStream = resource.OpenContent();
            if (textureStream == null)
                return null;
            using var image = TryCreate(textureStream);
            if (image == null)
                return null;

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
                fixed (void* dataBytePtr = image.Data)
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
    }
}
