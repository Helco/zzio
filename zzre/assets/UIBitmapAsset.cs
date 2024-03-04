using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using Silk.NET.SDL;
using Veldrid;
using zzio;
using zzio.vfs;
using zzre.game;
using zzre.materials;
using PixelFormat = Veldrid.PixelFormat;

namespace zzre;

public class UIBitmapAsset : Asset
{
    private const string UnmaskedSuffix = ".bmp";
    private const string ColorSuffix = "T.bmp";
    private const string MaskSuffix = "M.bmp";
    private const string RawMaskSuffix = "M.raw";
    private static readonly FilePath BasePath = new("resources/bitmaps");

    public readonly record struct Info(string Name, bool HasRawMask = false);

    public static void Register() =>
        AssetInfoRegistry<Info>.Register<UIBitmapAsset>(AssetLocality.Context);

    private readonly Info info;
    protected UIMaterial? material;

    public string DebugName { get; }
    public UIMaterial Material => material ??
        throw new InvalidOperationException("Asset was not yet loaded");
    public Vector2 Size => material is null ? Vector2.Zero
        : new Vector2(material.MainTexture.Texture!.Width, material.MainTexture.Texture!.Height);

    public UIBitmapAsset(IAssetRegistry registry, Guid assetId, Info info) : this(registry, assetId, info, null) { }
    public UIBitmapAsset(IAssetRegistry registry, Guid assetId, Info info, string? debugName) : base(registry, assetId)
    {
        this.info = info;
        DebugName = debugName ?? ($"UIBitmap {info.Name}" + (info.HasRawMask ? "" : " (Raw mask)"));
    }

    // strictly speaking this is a workaround: waiting on global secondary assets 
    // from local primary ones currently does not work and will throw an exception
    // practically we do not need this functionality:
    //   - We do not wait for textures (Model/Effect materials) 
    //   - We don't have to wait for samplers
    protected override bool NeedsSecondaryAssets => false;

    protected override ValueTask<IEnumerable<AssetHandle>> Load()
    {
        var graphicsDevice = diContainer.GetTag<GraphicsDevice>();
        using var bitmap = LoadMaskedBitmap(info.Name);
        var texture = bitmap.ToTexture(graphicsDevice, DebugName);
        var samplerAsset = Registry.LoadSampler(SamplerDescription.Linear);
        material = new UIMaterial(diContainer)
        {
            DebugName = DebugName,
            HasMask = info.HasRawMask
        };
        material.MainTexture.Texture = texture;
        material.MainSampler.Sampler = samplerAsset.Get().Sampler;
        material.ScreenSize.Buffer = diContainer.GetTag<UI>().ProjectionBuffer;

        if (info.HasRawMask)
        {
            var mask = LoadRawMask(bitmap.Width, bitmap.Height);
            var maskTexture = graphicsDevice.ResourceFactory.CreateTexture(new(
                (uint)bitmap.Width, (uint)bitmap.Height, depth: 1,
                mipLevels: 1, arrayLayers: 1,
                format: PixelFormat.R8_UInt,
                usage: TextureUsage.Sampled,
                type: TextureType.Texture2D));
            graphicsDevice.UpdateTexture(maskTexture, mask, 0, 0, 0, maskTexture.Width, maskTexture.Height, 1, 0, 0);
            material.MaskTexture.Texture = maskTexture;
        }

        return ValueTask.FromResult<IEnumerable<AssetHandle>>([ samplerAsset ]);
    }

    protected unsafe SdlSurfacePtr LoadMaskedBitmap(string name)
    {
        var sdl = diContainer.GetTag<Sdl>();
        var resourcePool = diContainer.GetTag<IResourcePool>();
        var bitmap =
            LoadBitmap(name, UnmaskedSuffix, withAlpha: true) ??
            LoadBitmap(name, ColorSuffix, withAlpha: true) ??
            throw new FileNotFoundException($"Could not open bitmap {name}");

        using var maskBitmap = LoadBitmap(name, MaskSuffix, withAlpha: null);
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

    private unsafe SdlSurfacePtr? LoadBitmap(string name, string suffix, bool? withAlpha)
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

    private byte[] LoadRawMask(int width, int height)
    {
        var resourcePool = diContainer.GetTag<IResourcePool>();
        var maskPath = BasePath.Combine(info.Name + RawMaskSuffix);
        using var stream = resourcePool.FindAndOpen(maskPath) ??
            throw new FileNotFoundException($"Could not open mask {maskPath}");
        if (stream.Length != width * height)
            throw new InvalidDataException($"Expected {width * height} bytes for a bitmap mask, but got {stream.Length}");
        var mask = new byte[width * height];
        stream.ReadExactly(mask.AsSpan());
        return mask;
    }

    protected override void Unload()
    {
        // UIBitmap (and UITileSheet) contain their textures without secondary assets
        // that is because sharing textures across UI materials does not exist
        material?.MainTexture.Texture?.Dispose();
        material?.MaskTexture.Texture?.Dispose();
        material?.Dispose();
        material = null;
    }

    public override string ToString() => DebugName;
}

partial class AssetExtensions
{
    public static unsafe AssetHandle<UIBitmapAsset> LoadUIBitmap(this IAssetRegistry registry,
        DefaultEcs.Entity entity,
        string name,
        bool hasRawMask = false,
        AssetLoadPriority priority = AssetLoadPriority.Synchronous)
    {
        var handle = registry.Load(new UIBitmapAsset.Info(name, hasRawMask), priority, &ApplyUIBitmapToEntity, entity);
        entity.Set(handle);
        return handle.As<UIBitmapAsset>();
    }

    private static void ApplyUIBitmapToEntity(AssetHandle handle, ref readonly DefaultEcs.Entity entity)
    {
        if (entity.IsAlive)
            entity.Set(handle.Get<UIBitmapAsset>().Material);
    }
}
