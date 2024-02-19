using System.IO;
using DefaultEcs.Resource;
using Silk.NET.SDL;
using Veldrid;
using zzio;
using zzio.vfs;
using zzre.rendering;

namespace zzre.game.resources;

public class UIBitmap : AResourceManager<string, materials.UIMaterial>
{
    private const string UnmaskedSuffix = ".bmp";
    private const string ColorSuffix = "T.bmp";
    private const string MaskSuffix = "M.bmp";
    private static readonly FilePath BasePath = new("resources/bitmaps");

    private readonly ITagContainer diContainer;
    private readonly Sdl sdl;
    private readonly UI ui;
    private readonly GraphicsDevice graphicsDevice;
    private readonly IResourcePool resourcePool;

    public UIBitmap(ITagContainer diContainer)
    {
        this.diContainer = diContainer;
        sdl = diContainer.GetTag<Sdl>();
        ui = diContainer.GetTag<UI>();
        graphicsDevice = diContainer.GetTag<GraphicsDevice>();
        resourcePool = diContainer.GetTag<IResourcePool>();
        Manage(diContainer.GetTag<DefaultEcs.World>());
    }

    protected override materials.UIMaterial Load(string name)
    {
        using var bitmap = LoadMaskedBitmap(sdl, resourcePool, name);
        var texture = bitmap.ToTexture(graphicsDevice);
        texture.Name = "UIBitmap " + name;
        var material = new materials.UIMaterial(diContainer);
        material.MainTexture.Texture = texture;
        material.MainSampler.Sampler = graphicsDevice.LinearSampler;
        material.ScreenSize.Buffer = ui.ProjectionBuffer;
        return material;
    }

    protected override void OnResourceLoaded(in DefaultEcs.Entity entity, string info, materials.UIMaterial resource)
    {
        entity.Set(resource);
    }

    protected override void Unload(string info, materials.UIMaterial resource)
    {
        resource.MainTexture.Texture?.Dispose();
        resource.Dispose();
    }

    internal static unsafe SdlSurfacePtr LoadMaskedBitmap(Sdl sdl, IResourcePool resourcePool, string name)
    {
        var bitmap =
            LoadBitmap(sdl, resourcePool, name, UnmaskedSuffix, withAlpha: true) ??
            LoadBitmap(sdl, resourcePool, name, ColorSuffix, withAlpha: true) ??
            throw new System.IO.FileNotFoundException($"Could not open bitmap {name}");

        using var maskBitmap = LoadBitmap(sdl, resourcePool, name, MaskSuffix, withAlpha: null);
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

    internal static unsafe SdlSurfacePtr? LoadBitmap(Sdl sdl, IResourcePool resourcePool, string name, string suffix, bool? withAlpha)
    {
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
            throw new System.NotSupportedException("ZZIO does not support surface pitch values other than Bpp*Width");

        return new(sdl, rawPointer);
    }
}
