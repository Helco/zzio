using System.Numerics;
using zzre.rendering;

namespace zzre.game;

public static class EntityAssetExtensions
{
    public static Vector2 LoadUIBitmapFor(this IAssetRegistry registry, DefaultEcs.Entity entity,
        string bitmap, bool hasRawMask = false)
    {
        var handle = registry.LoadUIBitmap(bitmap, hasRawMask);
        var asset = handle.Get();
        entity.Set(handle.As());
        entity.Set(asset.Material);
        return asset.Size;
    }

    public static TileSheet LoadUITileSheetFor(this IAssetRegistry registry, DefaultEcs.Entity entity,
        in UITileSheetAsset.Info info)
    {
        var handle = registry.LoadUITileSheet(info);
        var asset = handle.Get();
        entity.Set(handle.As());
        entity.Set(asset.Material);
        entity.Set(asset.TileSheet);
        return asset.TileSheet;
    }
    
    public static TileSheet LoadUITileSheetFor(this IAssetRegistry registry, ref DefaultEcs.Command.EntityRecord entity,
        in UITileSheetAsset.Info info)
    {
        var handle = registry.LoadUITileSheet(info);
        var asset = handle.Get();
        entity.Set(handle.As());
        entity.Set(asset.Material);
        entity.Set(asset.TileSheet);
        return asset.TileSheet;        
    }
}
