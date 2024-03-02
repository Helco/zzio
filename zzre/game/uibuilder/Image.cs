using System;
using System.Numerics;
using DefaultEcs;

namespace zzre.game.uibuilder;

internal sealed record Image : Base<Image>
{
    private components.ui.FullAlignment alignment = components.ui.FullAlignment.TopLeft;
    private string? bitmap;
    private UITileSheetAsset.Info? tileSheet;
    private int tileI = -1;

    public Image(UIBuilder preload, Entity parent) : base(preload, parent)
    {
    }

    public Image With(components.ui.FullAlignment alignment)
    {
        this.alignment = alignment;
        return this;
    }
    
    private void CheckNoMaterial()
    {
        if (bitmap != null || tileSheet != null)
            throw new InvalidOperationException("Image material is already set");
    }

    public Image WithBitmap(string bitmap)
    {
        CheckNoMaterial();
        this.bitmap = bitmap;
        return this;
    }

    public Image With(UITileSheetAsset.Info tileSheet, int tileI = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tileI);
        CheckNoMaterial();
        this.tileSheet = tileSheet;
        this.tileI = tileI;
        return this;
    }

    public Image With(zzio.CardId cardId) =>
        With(UIBuilder.GetTileSheetByCardType(cardId.Type), cardId.EntityId);

    public static implicit operator Entity(Image builder) => builder.Build();

    public Entity Build()
    {
        var entity = BuildBase();
        var assetRegistry = preload.UI.GetTag<IAssetRegistry>();

        Vector2 size;
        if (bitmap == null && tileSheet == null)
        {
            entity.Set<materials.UIMaterial>(null!); // untextured
            size = rect.Size;
        }
        else if (bitmap != null)
        {
            var handle = assetRegistry.LoadUIBitmap(entity, bitmap);
            size = handle.Get().Size;
        }
        else // if (tileSheet != null)
        {
            var handle = assetRegistry.LoadUITileSheet(entity, tileSheet!.Value);
            size = handle.Get().TileSheet.GetPixelSize(tileI);
        }
        AlignToSize(entity, size, alignment);
        entity.Set(new components.ui.Tile[] { new(tileI, rect) });

        return entity;
    }
}
