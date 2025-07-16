using System.Threading;
using System.Threading.Tasks;
using Veldrid;
using zzre.game;
using zzre.materials;
using zzre.rendering;
using PixelFormat = Veldrid.PixelFormat;

namespace zzre;

public sealed class UITileSheetAsset(IAssetRegistry registry) : IAsset<UITileSheetAsset.Info>
{
    private static readonly SamplerDescription SamplerDescription = new(
        SamplerAddressMode.Clamp, // the standard linear sampler uses Wrap
        SamplerAddressMode.Clamp,
        SamplerAddressMode.Clamp,
        SamplerFilter.MinLinear_MagLinear_MipLinear,
        comparisonKind: null,
        0, 0, 0, 0, SamplerBorderColor.TransparentBlack);

    public readonly record struct Info(
        string Name,
        float? LineHeight = null, // set any of these to make this asset a font
        float? LineOffset = null,
        float? CharSpacing = null);

    static AssetLocality IAsset.Locality => AssetLocality.Local;

    private AssetHandle<SamplerAsset> samplerHandle;

    public IAssetRegistry Registry => registry;
    public UIMaterial Material { get; private set; } = null!;
    public TileSheet TileSheet { get; private set; } = null!;

    static async Task<AssetLoadResult<Info>> IAsset<Info>.LoadAsync(IAssetRegistry registry, Info info, CancellationToken ct)
    {
        var samplerHandle = registry.LoadSampler(SamplerDescription, AssetPriority.High);
        var isFont = (info.LineHeight ?? info.LineOffset ?? info.CharSpacing) is not null;
        var debugName = $"UITileSheet {info.Name}" + (isFont ? " (font)" : "");
        var diContainer = registry.DIContainer;
        using var bitmap = UIBitmapAsset.LoadMaskedBitmap(diContainer, info.Name);

        var tileSheet = new TileSheet(info.Name, bitmap, isFont);
        if (info.LineHeight is float lineHeight)
            tileSheet.LineHeight = lineHeight;
        if (info.LineOffset is float lineOffset)
            tileSheet.LineOffset = lineOffset;
        if (info.CharSpacing is float charSpacing)
            tileSheet.CharSpacing = charSpacing;

        var graphicsDevice = diContainer.GetTag<GraphicsDevice>();
        var texture = graphicsDevice.ResourceFactory.CreateTexture(
            new TextureDescription(
                (uint)bitmap.Width,
                (uint)(bitmap.Height - 1),
                depth: 1,
                mipLevels: 1,
                arrayLayers: 1,
                PixelFormat.R8_G8_B8_A8_UNorm,
                TextureUsage.Sampled,
                TextureType.Texture2D));
        texture.Name = debugName;
        graphicsDevice.UpdateTexture(texture,
            bitmap.Data[(bitmap.Width * 4)..],
            0, 0, 0,
            width: texture.Width, height: texture.Height, depth: 1,
            mipLevel: 0, arrayLayer: 0);

        var material = new UIMaterial(diContainer)
        {
            DebugName = debugName,
            IsFont = isFont
        };
        material.MainTexture.Texture = texture;
        material.MainSampler.Sampler = (await samplerHandle.GetAsync(ct)).Sampler;
        material.ScreenSize.Buffer = diContainer.GetTag<UI>().ProjectionBuffer;
        return new(new UITileSheetAsset(registry)
        {
            samplerHandle = samplerHandle,
            Material = material,
            TileSheet = tileSheet
        });
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
        TileSheet = null!;
    }

    public override string ToString() => Material.DebugName;
}

static partial class AssetExtensions
{
    public static AssetHandle<UITileSheetAsset> LoadUITileSheet(this IAssetRegistry registry,
        in UITileSheetAsset.Info info,
        AssetPriority priority = AssetPriority.Synchronous) =>
        registry.Load<UITileSheetAsset.Info, UITileSheetAsset>(info, priority);
}

