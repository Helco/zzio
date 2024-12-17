using zzio;

using static zzre.game.systems.ui.InGameScreen;
namespace zzre.game.systems.ui;

public partial class ScrDeck : BaseScreen<components.ui.ScrDeck, messages.ui.OpenDeck>
{
    private void DragCard(DefaultEcs.Entity entity, ref components.ui.ScrDeck deck, InventoryCard card)
    {
        deck.DraggedCard = card;

        if (deck.DraggedCardImage != default) deck.DraggedCardImage.Dispose();
        deck.DraggedCardImage = preload.CreateImage(entity)
            .With(Mid)
            .With(card.cardId)
            .WithRenderOrder(-2)
            .Build();
        deck.DraggedCardImage.Set(new components.ui.DraggedCard(card));
        deck.DraggedCardImage.Set(components.ui.UIOffset.GameUpperLeft);

        if (deck.DraggedOverlay != default) deck.DraggedOverlay.Dispose();
        deck.DraggedOverlay = preload.CreateImage(entity)
            .With(Mid)
            .With(UIPreloadAsset.Dnd000, 0)
            .WithRenderOrder(-3)
            .Build();
        deck.DraggedOverlay.Set(components.ui.UIOffset.GameUpperLeft);
    }

    private static void DropCard(ref components.ui.ScrDeck deck)
    {
        deck.DraggedCard = default;
        deck.DraggedCardImage.Dispose();
        deck.DraggedCardImage = default;
        deck.DraggedOverlay.Dispose();
        deck.DraggedOverlay = default;
    }

    private void Drag(ref components.ui.ScrDeck deck)
    {
        if (deck.DraggedCardImage != default)
        {
            DragImage(deck.DraggedCardImage);
            DragImage(deck.DraggedOverlay);
        }
    }

    private void DragImage(DefaultEcs.Entity entity)
    {
        var tiles = entity.Get<components.ui.Tile[]>();
        tiles[0].Rect = tiles[0].Rect with { Center = ui.CursorEntity.Get<Rect>().Center };
    }
}
