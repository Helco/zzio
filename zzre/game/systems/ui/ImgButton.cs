using System;
using DefaultEcs.System;

namespace zzre.game.systems.ui
{
    public partial class ImgButton : AEntitySetSystem<float>
    {
        private readonly IDisposable addedSubscription;

        public ImgButton(ITagContainer diContainer) : base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, useBuffer: false)
        {
            addedSubscription = World.SubscribeComponentAdded<components.ui.ImgButtonTiles>(HandleComponentAdded);
        }

        private void HandleComponentAdded(in DefaultEcs.Entity entity, in components.ui.ImgButtonTiles tiles)
        {
            ref var rect = ref entity.Get<Rect>();
            if (rect.Size.MaxComponent() > 0f)
                return;

            var tileSheet = entity.Get<rendering.TileSheet>();
            rect.Size = tileSheet.GetPixelSize(tiles.Normal);

            if (!entity.Has<components.ui.Tile[]>())
                entity.Set(new components.ui.Tile[] { new(tiles.Normal, rect) });
        }

        [Update]
        private void Update(
            in DefaultEcs.Entity entity,
            in components.ui.Tile[] tiles,
            in components.ui.ImgButtonTiles imgButton)
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
