using System;
using System.Collections.Generic;
using Veldrid;
using zzre.game;
using zzre.materials;
using zzre.rendering;
using PixelFormat = Veldrid.PixelFormat;

namespace zzre;

public sealed class UITileSheetAsset : UIBitmapAsset
{
    private static readonly SamplerDescription SamplerDescription = new(
        SamplerAddressMode.Clamp, // the standard linear sampler uses Wrap
        SamplerAddressMode.Clamp,
        SamplerAddressMode.Clamp,
        SamplerFilter.MinLinear_MagLinear_MipLinear,
        comparisonKind: null,
        0, 0, 0, 0, SamplerBorderColor.TransparentBlack);

    public new readonly record struct Info(string Name, bool IsFont);

    public new static void Register() =>
        AssetInfoRegistry<Info>.Register<UITileSheetAsset>(AssetLocality.Context);

    private readonly Info info;
    private TileSheet? tileSheet;

    public TileSheet TileSheet => tileSheet ??
        throw new InvalidOperationException("Asset was not yet loaded");

    public UITileSheetAsset(IAssetRegistry registry, Guid assetId, Info info) : base(registry, assetId, new(info.Name),
        $"UITileSheet {info.Name}" + (info.IsFont ? " (font)" : ""))
    {
        this.info = info;
    }

    protected override IEnumerable<AssetHandle> Load()
    {
        using var bitmap = LoadMaskedBitmap(info.Name);
        tileSheet = new TileSheet(info.Name, bitmap, info.IsFont);

        var graphicsDevice = diContainer.GetTag<GraphicsDevice>();
        var samplerHandle = Registry.LoadSampler(SamplerDescription);
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
        texture.Name = DebugName;
        graphicsDevice.UpdateTexture(texture, bitmap.Data[(bitmap.Width * 4)..],
            0, 0, 0, width: texture.Width, height: texture.Height, depth: 1, mipLevel: 0, arrayLayer: 0);

        material = new UIMaterial(diContainer)
        {
            DebugName = DebugName,
            IsFont = info.IsFont
        };
        material.MainTexture.Texture = texture;
        material.MainSampler.Sampler = samplerHandle.Get().Sampler;
        material.ScreenSize.Buffer = diContainer.GetTag<UI>().ProjectionBuffer;
        return [ samplerHandle ];
    }

    protected override void Unload()
    {
        base.Unload();
        tileSheet = null;
    }
}

partial class AssetExtensions
{
    public static unsafe AssetHandle<UITileSheetAsset> LoadUITileSheet(this IAssetRegistry registry,
        DefaultEcs.Entity entity,
        in UITileSheetAsset.Info info,
        AssetLoadPriority priority = AssetLoadPriority.Synchronous)
    {
        var handle = registry.Load(info, priority, &ApplyUITileSheetToEntity, entity);
        entity.Set(handle);
        return handle.As<UITileSheetAsset>();
    }

    private static void ApplyUITileSheetToEntity(AssetHandle handle, ref readonly DefaultEcs.Entity entity)
    {
        if (entity.IsAlive)
        {
            var asset = handle.Get<UITileSheetAsset>();
            entity.Set(asset.Material);
            entity.Set(asset.TileSheet);
        }
    }

    public static AssetHandle<UITileSheetAsset> LoadUITileSheet(this IAssetRegistry registry,
        in DefaultEcs.Command.EntityRecord entity,
        in UITileSheetAsset.Info info)
    {
        // as the EntityRecord will be invalidated,
        // loading tilesheets into a record can only be done synchronously
        var handle = registry.Load(info, AssetLoadPriority.Synchronous);
        entity.Set(handle);

        var asset = handle.Get<UITileSheetAsset>();
        entity.Set(asset.Material);
        entity.Set(asset.TileSheet);
        return handle.As<UITileSheetAsset>();
    }
}

