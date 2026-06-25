using System;
using System.IO;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Silk.NET.SDL;
using Veldrid;
using zzio;
using zzio.vfs;
using zzre.game;
using zzre.materials;
using PixelFormat = Veldrid.PixelFormat;

namespace zzre;

public sealed class UIBitmapAsset(IAssetRegistry registry, UIBitmapAsset.Info info)
    : IAsset<UIBitmapAsset.Info>
{
    private const string UnmaskedSuffix = ".bmp";
    private const string ColorSuffix = "T.bmp";
    private const string MaskSuffix = "M.bmp";
    private const string RawMaskSuffix = "M.raw";
    private static readonly FilePath BasePath = new("resources/bitmaps");

    public readonly record struct Info(string Name, bool HasRawMask = false);

    static AssetLocality IAsset.Locality => AssetLocality.Local; // we set the UI projection matrix

    private readonly Info info = info;
    private AssetHandle<SamplerAsset> samplerHandle;

    public IAssetRegistry Registry => registry;
    public UIMaterial Material { get; private set; } = null!;
    public Vector2 Size => new(Material.MainTexture.Texture!.Width, Material.MainTexture.Texture!.Height);

    static async Task<AssetLoadResult<Info>> IAsset<Info>.LoadAsync(IAssetRegistry registry, Guid _, Info info, CancellationToken ct)
    {
        var debugName = $"UIBitmap {info.Name}" + (info.HasRawMask ? "" : " (Raw mask)");
        var samplerAsset = registry.LoadSampler(SamplerDescription.Linear, AssetPriority.High);
        var diContainer = registry.DIContainer;
        var graphicsDevice = diContainer.GetTag<GraphicsDevice>();
        using var bitmap = LoadMaskedBitmap(diContainer, info.Name);
        var texture = bitmap.ToTexture(graphicsDevice, debugName);
        var material = new UIMaterial(diContainer)
        {
            DebugName = debugName,
            HasMask = info.HasRawMask
        };
        material.MainTexture.Texture = texture;
        material.MainSampler.Sampler = (await samplerAsset.GetAsync(ct)).Sampler;
        material.ScreenSize.Buffer = diContainer.GetTag<UI>().ProjectionBuffer;

        if (info.HasRawMask)
        {
            var mask = LoadRawMask(diContainer, info.Name, bitmap.Width, bitmap.Height);
            var maskTexture = graphicsDevice.ResourceFactory.CreateTexture(new(
                (uint)bitmap.Width, (uint)bitmap.Height, depth: 1,
                mipLevels: 1, arrayLayers: 1,
                format: PixelFormat.R8_UInt,
                usage: TextureUsage.Sampled,
                type: TextureType.Texture2D));
            graphicsDevice.UpdateTexture(maskTexture, mask, 0, 0, 0, maskTexture.Width, maskTexture.Height, 1, 0, 0);
            material.MaskTexture.Texture = maskTexture;
        }

        return new(new UIBitmapAsset(registry, info)
        {
            samplerHandle = samplerAsset,
            Material = material
        });
    }

    internal static unsafe SdlSurfacePtr LoadMaskedBitmap(ITagContainer diContainer, string name)
    {
        var sdl = diContainer.GetTag<Sdl>();
        var resourcePool = diContainer.GetTag<IResourcePool>();
        var bitmap =
            LoadBitmap(diContainer, name, UnmaskedSuffix, withAlpha: true) ??
            LoadBitmap(diContainer, name, ColorSuffix, withAlpha: true) ??
            throw new FileNotFoundException($"Could not open bitmap {name}");

        using var maskBitmap = LoadBitmap(diContainer, name, MaskSuffix, withAlpha: null);
        if (maskBitmap == null)
            return bitmap;
        if (bitmap.Width != maskBitmap?.Width || bitmap.Height != maskBitmap?.Height)
            throw new InvalidDataException("Mask bitmap size does not match color bitmap");

        var maskBpp = maskBitmap.Value.Surface->Format->BytesPerPixel;
        var bitmapPixels = (byte*)bitmap.Surface->Pixels;
        var maskBitmapPixels = (byte*)maskBitmap.Value.Surface->Pixels;
        for (int i = 0; i < bitmap.Width * bitmap.Height; i++)
            bitmapPixels[i * 4 + 3] = maskBitmapPixels[i * maskBpp];
        return bitmap;
    }

    private static unsafe SdlSurfacePtr? LoadBitmap(ITagContainer diContainer, string name, string suffix, bool? withAlpha)
    {
        var sdl = diContainer.GetTag<Sdl>();
        var resourcePool = diContainer.GetTag<IResourcePool>();
        var path = BasePath.Combine(name + suffix);
        var fileBuffer = resourcePool.FindAndRead(path);
        if (fileBuffer == null)
            return null;
        var rwops = sdl.RWFromConstMem(fileBuffer);

        var rawPointer = sdl.LoadBMPRW(rwops, freesrc: 1);
        if (rawPointer == null)
            return null;

        var curFormat = rawPointer->Format->Format;
        uint targetFormat = withAlpha switch
        {
            true => Sdl.PixelformatAbgr8888,
            false => Sdl.PixelformatBgr888,
            null when curFormat == Sdl.PixelformatAbgr8888 || curFormat == Sdl.PixelformatBgr888 => curFormat,
            _ => Sdl.PixelformatAbgr8888
        };
        if (rawPointer->Format->Format != targetFormat)
        {
            var newPointer = sdl.ConvertSurfaceFormat(rawPointer, targetFormat, flags: 0);
            sdl.FreeSurface(rawPointer);
            if (newPointer == null)
                return null;
            rawPointer = newPointer;
        }
        if (rawPointer->Pitch != rawPointer->W * rawPointer->Format->BytesPerPixel)
            throw new NotSupportedException("ZZIO does not support surface pitch values other than Bpp*Width");

        return new(sdl, rawPointer);
    }

    private static byte[] LoadRawMask(ITagContainer diContainer, string name, int width, int height)
    {
        var resourcePool = diContainer.GetTag<IResourcePool>();
        var maskPath = BasePath.Combine(name + RawMaskSuffix);
        using var stream = resourcePool.FindAndOpen(maskPath) ??
            throw new FileNotFoundException($"Could not open mask {maskPath}");
        if (stream.Length != width * height)
            throw new InvalidDataException($"Expected {width * height} bytes for a bitmap mask, but got {stream.Length}");
        var mask = new byte[width * height];
        stream.ReadExactly(mask.AsSpan());
        return mask;
    }

    public void Dispose()
    {
        // UIBitmap (and UITileSheet) contain their textures without secondary assets
        // that is because sharing textures across UI materials does not exist
        Material?.MainTexture.Texture?.Dispose();
        Material?.MaskTexture.Texture?.Dispose();
        Material?.Dispose();
        Material = null!;
        samplerHandle.Dispose();
    }

    public override string ToString() => Material.DebugName;
}

static partial class AssetExtensions
{
    public static AssetHandle<UIBitmapAsset> LoadUIBitmap(this IAssetRegistry registry,
        string name,
        bool hasRawMask = false,
        AssetPriority priority = AssetPriority.Synchronous) =>
        registry.Load<UIBitmapAsset.Info, UIBitmapAsset>(new(name, hasRawMask), priority);
}
