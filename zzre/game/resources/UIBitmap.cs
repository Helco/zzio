using System.IO;
using DefaultEcs.Resource;
using StbImageSharp;
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
    private readonly UI ui;
    private readonly GraphicsDevice graphicsDevice;
    private readonly IResourcePool resourcePool;

    public UIBitmap(ITagContainer diContainer)
    {
        this.diContainer = diContainer;
        ui = diContainer.GetTag<UI>();
        graphicsDevice = diContainer.GetTag<GraphicsDevice>();
        resourcePool = diContainer.GetTag<IResourcePool>();
        Manage(diContainer.GetTag<DefaultEcs.World>());
    }

    protected override materials.UIMaterial Load(string name)
    {
        var texture = LoadMaskedBitmap(resourcePool, name).ToTexture(graphicsDevice);
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

    internal static ImageResult LoadMaskedBitmap(IResourcePool resourcePool, string name)
    {
        var bitmap =
            LoadBitmap(resourcePool, name, UnmaskedSuffix, withAlpha: true) ??
            LoadBitmap(resourcePool, name, ColorSuffix, withAlpha: true) ??
            throw new System.IO.FileNotFoundException($"Could not open bitmap {name}");

        var maskBitmap = LoadBitmap(resourcePool, name, MaskSuffix, withAlpha: null);
        if (maskBitmap == null)
            return bitmap;
        if (bitmap.Width != maskBitmap.Width || bitmap.Height != maskBitmap.Height)
            throw new InvalidDataException("Mask bitmap size does not match color bitmap");

        var maskBpp = maskBitmap.ColorComponents.Count();
        for (int i = 0; i < bitmap.Width * bitmap.Height; i++)
            bitmap.Data[i * 4 + 3] = maskBitmap.Data[i * maskBpp];
        return bitmap;
    }

    internal static ImageResult? LoadBitmap(IResourcePool resourcePool, string name, string suffix, bool? withAlpha)
    {
        var path = BasePath.Combine(name + suffix);
        using var stream = resourcePool.FindAndOpen(path);
        if (stream == null)
            return null;
        var result = StbImageSharp.Decoding.BmpDecoder.Decode(stream, withAlpha switch
        {
            null => null,
            true => ColorComponents.RedGreenBlueAlpha,
            false => ColorComponents.RedGreenBlue
        }) ?? throw new System.IO.InvalidDataException($"Could not decode bitmap {name}{suffix}");
        return result;
    }
}
