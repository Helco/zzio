using DefaultEcs.Resource;
using Veldrid;
using Veldrid.ImageSharp;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using zzio;
using zzio.vfs;

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
    private readonly ResourceFactory resourceFactory;
    private readonly IResourcePool resourcePool;

    public UIBitmap(ITagContainer diContainer)
    {
        this.diContainer = diContainer;
        ui = diContainer.GetTag<UI>();
        graphicsDevice = diContainer.GetTag<GraphicsDevice>();
        resourceFactory = diContainer.GetTag<ResourceFactory>();
        resourcePool = diContainer.GetTag<IResourcePool>();
        Manage(diContainer.GetTag<DefaultEcs.World>());
    }

    protected override materials.UIMaterial Load(string name)
    {
        using var bitmap = LoadMaskedBitmap(resourcePool, name);
        var texture = new ImageSharpTexture(bitmap, mipmap: false)
            .CreateDeviceTexture(graphicsDevice, resourceFactory);
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

    internal static Image<Rgba32> LoadMaskedBitmap(IResourcePool resourcePool, string name)
    {
        var bitmap =
            LoadBitmap<Rgba32>(resourcePool, name, UnmaskedSuffix) ??
            LoadBitmap<Rgba32>(resourcePool, name, ColorSuffix) ??
            throw new System.IO.FileNotFoundException($"Could not open bitmap {name}");

        using var maskBitmap = LoadBitmap<Rgb24>(resourcePool, name, MaskSuffix);
        if (maskBitmap == null)
            return bitmap;

        for (int y = 0; y < bitmap.Height; y++)
        {
            var colorSpan = bitmap.GetPixelRowSpan(y);
            var maskSpan = maskBitmap.GetPixelRowSpan(y);
            for (int x = 0; x < bitmap.Width; x++)
                colorSpan[x].A = maskSpan[x].R;
        }
        return bitmap;
    }

    internal static Image<TPixel>? LoadBitmap<TPixel>(IResourcePool resourcePool, string name, string suffix)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        var path = BasePath.Combine(name + suffix);
        using var stream = resourcePool.FindAndOpen(path);
        if (stream == null)
            return null;
        return Image.Load<TPixel>(stream);
    }
}
