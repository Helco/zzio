using System.Collections.Generic;
using DefaultEcs.Resource;
using Veldrid;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using zzio.vfs;
using zzre.materials;
using zzre.rendering;

namespace zzre.game.resources;

public record struct UITileSheetInfo(string Name, bool IsFont);

public class UITileSheet : AResourceManager<UITileSheetInfo, TileSheet>, System.IDisposable
{
    private readonly Dictionary<TileSheet, UIMaterial> materials = new();
    private readonly ITagContainer diContainer;
    private readonly UI ui;
    private readonly GraphicsDevice graphicsDevice;
    private readonly ResourceFactory resourceFactory;
    private readonly IResourcePool resourcePool;
    private readonly Sampler linearSampler; // a linear, non-bleeding sampler
    private readonly Sampler fontSampler; // a linear, non-bleeding sampler

    public UITileSheet(ITagContainer diContainer)
    {
        this.diContainer = diContainer;
        ui = diContainer.GetTag<UI>();
        graphicsDevice = diContainer.GetTag<GraphicsDevice>();
        resourceFactory = diContainer.GetTag<ResourceFactory>();
        resourcePool = diContainer.GetTag<IResourcePool>();
        Manage(diContainer.GetTag<DefaultEcs.World>());

        linearSampler = resourceFactory.CreateSampler(new SamplerDescription(
            SamplerAddressMode.Clamp,
            SamplerAddressMode.Clamp,
            SamplerAddressMode.Clamp,
            SamplerFilter.MinLinear_MagLinear_MipLinear,
            comparisonKind: null,
            0, 0, 0, 0, SamplerBorderColor.TransparentBlack));

        fontSampler = resourceFactory.CreateSampler(new SamplerDescription(
            SamplerAddressMode.Clamp,
            SamplerAddressMode.Clamp,
            SamplerAddressMode.Clamp,
            SamplerFilter.MinLinear_MagLinear_MipLinear,
            comparisonKind: null,
            0, 0, 0, 0, SamplerBorderColor.TransparentBlack));
    }

    public new void Dispose()
    {
        base.Dispose();
        linearSampler.Dispose();
        fontSampler.Dispose();
        foreach (var material in materials.Values)
        {
            material.MainTexture?.Dispose();
            material.Dispose();
        }
    }

    protected override TileSheet Load(UITileSheetInfo info)
    {
        using var bitmap = UIBitmap.LoadMaskedBitmap(resourcePool, info.Name);
        var tileSheet = new TileSheet(info.Name, bitmap, info.IsFont);

        var texture = resourceFactory.CreateTexture(
            new TextureDescription(
                (uint)bitmap.Width,
                (uint)(bitmap.Height - 1),
                depth: 1,
                mipLevels: 1,
                arrayLayers: 1,
                PixelFormat.R8_G8_B8_A8_UNorm,
                TextureUsage.Sampled,
                TextureType.Texture2D));
        texture.Name = (info.IsFont ? "UIFont " : "UITileSheet ") + info.Name;
        UploadTileSheet(bitmap, texture, info.IsFont);

        var material = new UIMaterial(diContainer) { IsFont = info.IsFont };
        material.MainTexture.Texture = texture;
        material.MainSampler.Sampler = info.IsFont ? fontSampler : linearSampler;
        material.ScreenSize.Buffer = ui.ProjectionBuffer;
        materials.Add(tileSheet, material);
        return tileSheet;
    }

    protected override void OnResourceLoaded(in DefaultEcs.Entity entity, UITileSheetInfo info, TileSheet tileSheet)
    {
        entity.Set(materials[tileSheet]);
        entity.Set(tileSheet);
    }

    protected override void Unload(UITileSheetInfo info, TileSheet resource)
    {
        var material = materials[resource];
        material.MainTexture.Texture?.Dispose();
        material.Dispose();
        materials.Remove(resource);
    }

    private unsafe void UploadTileSheet(Image<Rgba32> bitmap, Texture texture, bool isFont)
    {
        // skipping the first row

        if (!bitmap.TryGetSinglePixelSpan(out var totalSpan) || totalSpan.Length != bitmap.Width * bitmap.Height)
            throw new System.ArgumentException("TileSheets can only be uploaded for contiguous bitmaps");

        fixed (Rgba32* src = totalSpan)
        {
            graphicsDevice.UpdateTexture(texture,
                new(src + bitmap.Width), (uint)totalSpan.Length * 4,
                x: 0, y: 0, z: 0,
                (uint)bitmap.Width, (uint)bitmap.Height - 1, depth: 1,
                mipLevel: 0,
                arrayLayer: 0);
        }
    }
}
