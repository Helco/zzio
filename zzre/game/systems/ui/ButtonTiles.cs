using System;
using DefaultEcs.System;

namespace zzre.game.systems.ui
{
    public partial class ButtonTiles : AEntitySetSystem<float>
    {
        private readonly IDisposable addedSubscription;

        public ButtonTiles(ITagContainer diContainer) : base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, useBuffer: false)
        {
            addedSubscription = World.SubscribeComponentAdded<components.ui.ButtonTiles>(HandleComponentAdded);
        }

        public override void Dispose()
        {
            base.Dispose();
            addedSubscription.Dispose();
        }

        private void HandleComponentAdded(in DefaultEcs.Entity entity, in components.ui.ButtonTiles tiles)
        {
            ref var rect = ref entity.Get<Rect>();
            if (MathEx.CmpZero(rect.Size.MaxComponent()))
            {
                var tileSheet = entity.Get<rendering.TileSheet>();
                rect.Size = tileSheet.GetPixelSize(tiles.Normal);
                rect.Center += rect.HalfSize + System.Numerics.Vector2.One /2; // the user intended to set a top-left position
            }

            if (!entity.Has<components.ui.Tile[]>())
                entity.Set(new components.ui.Tile[] { new(tiles.Normal, rect) });
        }

        [Update]
        private void Update(
            in DefaultEcs.Entity entity,
            in components.ui.Tile[] tiles,
            in components.ui.ButtonTiles imgButton)
        {
            bool isActive = entity.Has<components.ui.Active>();
            bool isHovered = entity.Has<components.ui.Hovered>();
            int newTileI = 0 switch
            {
                _ when isActive && isHovered && imgButton.ActiveHovered >= 0 => imgButton.ActiveHovered,
                _ when isActive => imgButton.Active,
                _ when isHovered && imgButton.Hovered >= 0 => imgButton.Hovered,
                _ => imgButton.Normal
            };
            foreach (ref var tile in tiles.AsSpan())
                tile.TileId = newTileI;
        }
    }
}
