using System;
using System.Numerics;
using DefaultEcs;
using DefaultEcs.Resource;
using zzre.game.systems.ui;

using TileSheetResource = DefaultEcs.Resource.ManagedResource<zzre.game.resources.UITileSheetInfo, zzre.rendering.TileSheet>;

namespace zzre.game.uibuilder;

internal sealed record Image : Base<Image>
{
    private components.ui.FullAlignment alignment = components.ui.FullAlignment.TopLeft;
    private string? bitmap;
    private TileSheetResource? tileSheet;
    private int tileI = -1;

    public Image(UIPreloader preload, Entity parent) : base(preload, parent)
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

    public Image With(TileSheetResource tileSheet, int tileI = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tileI);
        CheckNoMaterial();
        this.tileSheet = tileSheet;
        this.tileI = tileI;
        return this;
    }

    public Image With(zzio.CardId cardId) =>
        With(preload.GetTileSheetByCardType(cardId.Type), cardId.EntityId);

    public static implicit operator Entity(Image builder) => builder.Build();

    public Entity Build()
    {
        var entity = BuildBase();

        Vector2 size;
        if (bitmap == null && tileSheet == null)
        {
            entity.Set<materials.UIMaterial>(null!); // untextured
            size = rect.Size;
        }
        else if (bitmap != null)
        {
            entity.Set(ManagedResource<materials.UIMaterial>.Create(bitmap));
            var texture = entity.Get<materials.UIMaterial>().MainTexture.Texture!;
            size = new(texture.Width, texture.Height);
        }
        else // if (tileSheet != null)
        {
            entity.Set(tileSheet!.Value);
            size = entity.Get<rendering.TileSheet>().GetPixelSize(tileI);
        }
        AlignToSize(entity, size, alignment);
        entity.Set(new components.ui.Tile[] { new(tileI, rect) });

        return entity;
    }
}
