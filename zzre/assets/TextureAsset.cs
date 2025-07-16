using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Silk.NET.SDL;
using Veldrid;
using zzio;
using zzio.vfs;
using zzre.rendering;
using Texture = Veldrid.Texture;
using PixelFormat = Veldrid.PixelFormat;
using System.Threading;

namespace zzre;

public sealed class TextureAsset(IAssetRegistry registry, TextureAsset.Info info) : IAsset<TextureAsset.Info>
{
    public readonly record struct Info(FilePath FullPath)
    {
        public Info(string fullPath) : this(new FilePath(fullPath)) { }
    }

    private readonly Info info = info;

    public IAssetRegistry Registry { get; } = registry;
    public Texture Texture { get; private set; } = null!;

    static Task<AssetLoadResult<Info>> IAsset<Info>.LoadAsync(IAssetRegistry registry, Info info, CancellationToken ct)
    {
        var resourcePool = registry.DIContainer.GetTag<IResourcePool>();
        using var textureStream = resourcePool.FindAndOpen(info.FullPath) ??
            throw new FileNotFoundException($"Could not open texture {info.FullPath}");
        var texture = (info.FullPath.Extension ?? "").ToLowerInvariant() switch
        {
            "dds" => LoadFromDDS(registry.DIContainer, textureStream),
            "bmp" => LoadFromBMP(registry.DIContainer, textureStream),
            _ => throw new NotSupportedException($"Unsupported texture extension: {info.FullPath.Extension}")
        };
        texture.Name = info.FullPath.Parts[^1];
        return Task.FromResult(new AssetLoadResult<Info>(
            new TextureAsset(registry, info) { Texture = texture }
        ));
    }

    private static unsafe Texture LoadFromBMP(ITagContainer diContainer, Stream textureStream)
    {
        var sdl = diContainer.GetTag<Sdl>();
        var graphicsDevice = diContainer.GetTag<GraphicsDevice>();
        var imageBuffer = new byte[textureStream.Length];
        textureStream.ReadExactly(imageBuffer.AsSpan());
        var rwops = sdl.RWFromConstMem(imageBuffer);
        var rawPointer = sdl.LoadBMPRW(rwops, freesrc: 1);
        if (rawPointer == null)
            throw new InvalidDataException("Failed to load BMP");

        var curFormat = rawPointer->Format->Format;
        if (rawPointer->Format->Format != Sdl.PixelformatAbgr8888)
        {
            var newPointer = sdl.ConvertSurfaceFormat(rawPointer, Sdl.PixelformatAbgr8888, flags: 0);
            sdl.FreeSurface(rawPointer);
            if (newPointer == null)
                throw new InvalidOperationException("Failed to convert SDL surface to RGBA");
            rawPointer = newPointer;
        }
        if (rawPointer->Pitch != rawPointer->W * rawPointer->Format->BytesPerPixel)
            throw new NotSupportedException("ZZIO does not support surface pitch values other than Bpp*Width");

        using var image = new SdlSurfacePtr(sdl, rawPointer);
        return image.ToTexture(graphicsDevice, "UNSET NAME");
    }

    private static unsafe Texture LoadFromDDS(ITagContainer diContainer, Stream stream)
    {
        using var image = Pfim.Dds.Create(stream, new Pfim.PfimConfig());

        var textureFormat = TryConvertPixelFormat(image.Format) ??
            throw new NotSupportedException($"Unsupported DDS format {image.Format}");

        var graphicsDevice = diContainer.GetTag<GraphicsDevice>();
        var texture = graphicsDevice.ResourceFactory.CreateTexture(TextureDescription.Texture2D(
            width: (uint)image.Width,
            height: (uint)image.Height,
            mipLevels: (uint)image.MipMaps.Length + 1,
            arrayLayers: 1,
            textureFormat,
            TextureUsage.Sampled));

        fixed (void* dataBytePtr = image.Data)
        {
            IntPtr dataPtr = new(dataBytePtr);
            graphicsDevice.UpdateTexture(texture,
                source: dataPtr,
                sizeInBytes: (uint)image.DataLen,
                x: 0, y: 0, z: 0,
                width: texture.Width,
                height: texture.Height,
                depth: 1,
                mipLevel: 0,
                arrayLayer: 0);

            foreach (var (mipMap, level) in image.MipMaps.Indexed())
                graphicsDevice.UpdateTexture(texture,
                    source: dataPtr + mipMap.DataOffset,
                    sizeInBytes: (uint)mipMap.DataLen,
                    x: 0, y: 0, z: 0,
                    width: (uint)mipMap.Width,
                    height: (uint)mipMap.Height,
                    depth: 1,
                    mipLevel: (uint)level + 1,
                    arrayLayer: 0);
        }
        return texture;
    }

    private static PixelFormat? TryConvertPixelFormat(Pfim.ImageFormat img) => img switch
    {
        Pfim.ImageFormat.Rgb8 => PixelFormat.R8_UNorm,
        Pfim.ImageFormat.Rgba32 => PixelFormat.B8_G8_R8_A8_UNorm,
        _ => null
    };

    public void Dispose()
    {
        Texture?.Dispose();
        Texture = null!;
    }

    public override string ToString() => info.FullPath.Parts.Count > 1
        ? $"Texture {info.FullPath.Parts[^1]} ({info.FullPath.Parts[^2]})"
        : $"Texture {info.FullPath.ToPOSIXString()}";
}

static partial class AssetExtensions
{
    private static readonly IReadOnlyList<string> TextureExtensions = [".dds", ".bmp"];

    public static AssetHandle<TextureAsset>? TryLoadTexture(this IAssetRegistry registry,
        IReadOnlyList<FilePath> texturePaths,
        string textureName,
        AssetPriority priority)
    {
        var resourcePool = registry.DIContainer.GetTag<IResourcePool>();
        foreach (var texturePath in texturePaths)
        {
            foreach (var extension in TextureExtensions)
            {
                var path = texturePath.Combine(textureName + extension);
                if (resourcePool.FindFile(path) is not null)
                    return registry.LoadTexture(path, priority);
            }
        }
        return null;
    }

    public static AssetHandle<TextureAsset> LoadTexture(this IAssetRegistry registry,
        IReadOnlyList<FilePath> texturePaths,
        string textureName,
        AssetPriority priority) =>
        registry.TryLoadTexture(texturePaths, textureName, priority) ??
        throw new FileNotFoundException($"Could not find any texture \"{textureName}\"");

    public static AssetHandle<TextureAsset> LoadTexture(this IAssetRegistry registry,
        FilePath fullPath,
        AssetPriority priority) =>
        registry.Load<TextureAsset.Info, TextureAsset>(new(fullPath), priority);
}
