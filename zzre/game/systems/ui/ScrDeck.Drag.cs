using zzio;

using static zzre.game.systems.ui.InGameScreen;
namespace zzre.game.systems.ui;

public partial class ScrDeck : BaseScreen<components.ui.ScrDeck, messages.ui.OpenDeck>
{
    private void DragCard(DefaultEcs.Entity deckEntity, ref components.ui.ScrDeck deck, InventoryCard card)
    {
        deck.DraggedCard = card;

        if (deck.DraggedCardImage != default) deck.DraggedCardImage.Dispose();
        deck.DraggedCardImage = preload.CreateImage(deckEntity)
            .With(Mid)
            .With(card.cardId)
            .WithRenderOrder(-2)
            .Build();
        deck.DraggedCardImage.Set(new components.ui.DraggedCard(card));
        deck.DraggedCardImage.Set(components.ui.UIOffset.GameUpperLeft);

        if (deck.DraggedOverlay != default) deck.DraggedOverlay.Dispose();
        deck.DraggedOverlay = preload.CreateImage(deckEntity)
            .With(Mid)
            .With(UIPreloadAsset.Dnd000, 0)
            .WithRenderOrder(-3)
            .Build();
        deck.DraggedOverlay.Set(components.ui.UIOffset.GameUpperLeft);

        foreach (var slotEntity in deck.DeckSlots)
        {
            ref var slot = ref slotEntity.Get<components.ui.Slot>();
            SetDeckSlotTooltip(ref deck, ref slot);
        }
    }

    private void DropCard(ref components.ui.ScrDeck deck)
    {
        if (deck.VacatedDeckSlot != -1)
        {
            inventory.SetSlot((InventoryFairy)deck.DraggedCard!, deck.VacatedDeckSlot);
            SetDeckSlot(deck.DeckSlots[deck.VacatedDeckSlot], ref deck);
        }
        foreach (var slotEntity in deck.ListSlots)
            UnsetHoverMode(slotEntity);
        deck.VacatedDeckSlot = -1;
        deck.DraggedCard = default;
        deck.DraggedCardImage.Dispose();
        deck.DraggedCardImage = default;
        deck.DraggedOverlay.Dispose();
        deck.DraggedOverlay = default;
        SetListSlots(ref deck);

        foreach (var slotEntity in deck.DeckSlots)
        {
            ref var slot = ref slotEntity.Get<components.ui.Slot>();
            SetDeckSlotTooltip(ref deck, ref slot);
        }
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
