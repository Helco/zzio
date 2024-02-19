using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Silk.NET.SDL;
using Veldrid;
using zzio;
using zzio.vfs;
using zzre.rendering;
using PixelFormat = Veldrid.PixelFormat;
using Texture = Veldrid.Texture;

namespace zzre;

public class TextureAssetLoader : IAssetLoader<Texture>, IAssetLoaderValidation<Texture>
{
    public ITagContainer DIContainer { get; }
    private readonly IResourcePool resourcePool;
    private readonly GraphicsDevice device;
    private readonly Sdl sdl;

    public TextureAssetLoader(ITagContainer diContainer)
    {
        DIContainer = diContainer;
        resourcePool = diContainer.GetTag<IResourcePool>();
        device = diContainer.GetTag<GraphicsDevice>();
        sdl = diContainer.GetTag<Sdl>();
    }

    public bool TryLoad(IResource resource, [NotNullWhen(true)] out Texture? texture)
    {
        var extension = resource.Path.Extension?.ToLowerInvariant();
        texture = extension switch
        {
            "dds" => TryLoadDDSTexture(resource),
            "bmp" => TryLoadBMPTexture(resource),
            _ => throw new NotSupportedException($"Unsupported texture resource extension \"{extension}\"")
        };
        return texture != null;
    }

    public void Clear() { }

    private unsafe Texture? TryLoadBMPTexture(IResource resource)
    {
        using var textureStream = resource.OpenContent();
        if (textureStream == null)
            return null;
        try
        {
            var imageBuffer = new byte[textureStream.Length];
            textureStream.ReadExactly(imageBuffer.AsSpan());
            var rwops = sdl.RWFromConstMem(imageBuffer);
            var rawPointer = sdl.LoadBMPRW(rwops, freesrc: 1);
            if (rawPointer == null)
                return null;

            var curFormat = rawPointer->Format->Format;
            if (rawPointer->Format->Format != Sdl.PixelformatAbgr8888)
            {
                var newPointer = sdl.ConvertSurfaceFormat(rawPointer, Sdl.PixelformatAbgr8888, flags: 0);
                sdl.FreeSurface(rawPointer);
                if (newPointer == null)
                    return null;
                rawPointer = newPointer;
            }
            if (rawPointer->Pitch != rawPointer->W * rawPointer->Format->BytesPerPixel)
                throw new System.NotSupportedException("ZZIO does not support surface pitch values other than Bpp*Width");

            using var image = new SdlSurfacePtr(sdl, rawPointer);
            return image.ToTexture(device);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private Texture? TryLoadDDSTexture(IResource resource)
    {
        static Pfim.IImage? TryCreate(Stream stream)
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
                IntPtr dataPtr = new(dataBytePtr);
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

    private static PixelFormat? TryConvertPixelFormat(Pfim.ImageFormat img) => img switch
    {
        Pfim.ImageFormat.Rgb8 => PixelFormat.R8_UNorm,
        Pfim.ImageFormat.Rgba32 => PixelFormat.B8_G8_R8_A8_UNorm,
        _ => null
    };

    public void ValidateAsset(Texture asset)
    {
        if (asset.IsDisposed)
            throw new InvalidOperationException($"Texture {asset.Name} was disposed");
    }
}
