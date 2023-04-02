using System;
using System.Numerics;
using DefaultEcs.System;

namespace zzre.game.systems.ui;

public partial class Slider : AEntitySetSystem<float>
{
    private readonly IZanzarahContainer zzContainer;
    private readonly UI ui;
    private readonly IDisposable addedSubscription;

    public Slider(ITagContainer diContainer) : base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, useBuffer: false)
    {
        zzContainer = diContainer.GetTag<IZanzarahContainer>();
        ui = diContainer.GetTag<UI>();
        addedSubscription = World.SubscribeEntityComponentAdded<components.ui.Slider>(HandleAdded);
    }

    public override void Dispose()
    {
        base.Dispose();
        addedSubscription.Dispose();
    }

    private void HandleAdded(in DefaultEcs.Entity entity, in components.ui.Slider slider)
    {
        var tileSheet = entity.Get<rendering.TileSheet>();
        var buttonTiles = entity.Get<components.ui.ButtonTiles>();
        var tileSize = tileSheet.GetPixelSize(buttonTiles.Normal);
        var tiles = entity.Get<components.ui.Tile[]>();
        tiles[0].Rect = tiles[0].Rect with { Size = tileSize };
    }

    [Update]
    private void Update(
        in DefaultEcs.Entity entity,
        in Rect rect,
        in components.ui.UIOffset offset,
        ref components.ui.Slider slider,
        ref components.ui.Tile[] tiles)
    {
        var offsettedRect = new Rect(rect.Center, rect.Size * slider.SizeFactor);
        var isHovered = entity.Has<components.ui.Hovered>();
        if (zzContainer.IsMouseDown(Veldrid.MouseButton.Left) && (isHovered || slider.WasHovered))
        {
            var offsettedMousePos = offset.CalcReverse(zzContainer.MousePos, ui.LogicalScreen);
            slider.Current = Vector2.Clamp(offsettedRect.RelativePos(offsettedMousePos), Vector2.Zero, Vector2.One);
        }
        slider = slider with { WasHovered = isHovered };

        tiles[0].Rect = tiles[0].Rect with { Center = MathEx.Floor(offsettedRect.AbsolutePos(slider.Current)) };
    }
}
