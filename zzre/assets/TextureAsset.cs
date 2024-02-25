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

namespace zzre;

public sealed class TextureAsset : Asset
{
    public readonly record struct Info(FilePath FullPath)
    {
        public Info(string fullPath) : this(new FilePath(fullPath)) { }
    }

    public static void Register() =>
        AssetInfoRegistry<Info>.Register<TextureAsset>();

    private readonly FilePath path;
    private Texture? texture;

    public Texture Texture => texture ??
        throw new InvalidOperationException("Asset was not yet loaded");

    public TextureAsset(ITagContainer diContainer, Guid assetId, Info info) : base(diContainer, assetId)
    {
        path = info.FullPath;
    }

    protected override ValueTask<IEnumerable<AssetHandle>> Load()
    {
        var resourcePool = diContainer.GetTag<IResourcePool>();
        using var textureStream = resourcePool.FindAndOpen(path) ??
            throw new FileNotFoundException($"Could not open texture {path}");
        texture = (path.Extension ?? "").ToLowerInvariant() switch
        {
            "dds" => LoadFromDDS(textureStream),
            "bmp" => LoadFromBMP(textureStream),
            _ => throw new NotSupportedException($"Unsupported texture extension: {path.Extension}")
        };
        return NoSecondaryAssets;
    }

    private unsafe Texture? LoadFromBMP(Stream textureStream)
    {
        var sdl = diContainer.GetTag<Sdl>();
        var graphicsDevice = diContainer.GetTag<GraphicsDevice>();
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
            throw new NotSupportedException("ZZIO does not support surface pitch values other than Bpp*Width");

        using var image = new SdlSurfacePtr(sdl, rawPointer);
        return image.ToTexture(graphicsDevice);
    }

    private unsafe Texture? LoadFromDDS(Stream stream)
    {
        using var image = Pfim.Dds.Create(stream, new Pfim.PfimConfig());

        var textureFormat = TryConvertPixelFormat(image.Format);
        if (textureFormat == null)
            return null; // TODO: Support converting Pfim image formats to RGBA32

        var graphicsDevice = diContainer.GetTag<GraphicsDevice>();
        var texture = graphicsDevice.ResourceFactory.CreateTexture(TextureDescription.Texture2D(
            width: (uint)image.Width,
            height: (uint)image.Height,
            mipLevels: (uint)image.MipMaps.Length + 1,
            arrayLayers: 1,
            textureFormat.Value,
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

    protected override void Unload()
    {
        texture?.Dispose();
        texture = null;
    }
}

public static partial class AssetExtensions
{
    private static readonly IReadOnlyList<string> TextureExtensions = [".dds", ".bmp"];

    public static AssetHandle<TextureAsset> LoadTexture(this IAssetRegistry registry,
        IReadOnlyList<FilePath> texturePaths,
        string textureName,
        AssetLoadPriority priority)
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
        throw new FileNotFoundException($"Could not find any texture \"{textureName}\"");
    }

    public static AssetHandle<TextureAsset> LoadTexture(this IAssetRegistry registry,
        FilePath fullPath,
        AssetLoadPriority priority) =>
        registry.Load(new TextureAsset.Info(fullPath), priority).As<TextureAsset>();
}
