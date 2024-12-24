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
            .With(UIPreloadAsset.Dnd000)
            .WithRenderOrder(-3)
            .Build();
        deck.DraggedOverlay.Set(components.ui.UIOffset.GameUpperLeft);
        SetDragOverlayTile(ref deck);
        RefreshTooltips(ref deck);
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
        RefreshTooltips(ref deck);
    }

    private static void RefreshTooltips(ref components.ui.ScrDeck deck)
    {
        foreach (var slotEntity in deck.DeckSlots)
        {
            ref var slot = ref slotEntity.Get<components.ui.Slot>();
            SetDeckSlotTooltip(ref deck, ref slot);
            foreach (var spellSlotEntity in slot.spellSlots)
            {
                ref var spellSlot = ref spellSlotEntity.Get<components.ui.Slot>();
                SetSpellSlotTooltip(ref deck, ref slot, ref spellSlot);
            }
        }
    }

    private void SetDragOverlayTile(ref components.ui.ScrDeck deck)
    {
        if (deck.DraggedCard == default) return;
        deck.DraggedOverlay.Set(new components.ui.ButtonTiles(DragOverlayTile(ref deck)));
    }

    private int DragOverlayTile(ref components.ui.ScrDeck deck)
    {
        if (deck.LastHovered == default) return 1;
        if (!deck.LastHovered.Has<components.ui.SlotButton>()) return 1;
        var slotEntity = deck.LastHovered.Get<components.Parent>().Entity;
        ref var slot = ref slotEntity.Get<components.ui.Slot>();
        if (slot.type == components.ui.Slot.Type.DeckSlot && deck.DraggedCard!.cardId.Type != CardType.Spell)
            return 0;
        if (slot.type == components.ui.Slot.Type.SpellSlot && deck.DraggedCard!.cardId.Type == CardType.Spell && IsOfMatchingSpellType(ref slot, deck.DraggedCard!))
            return 0;
        return 1;
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
