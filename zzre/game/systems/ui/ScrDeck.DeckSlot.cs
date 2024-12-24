using System.Numerics;
using zzio;
using static zzre.game.systems.ui.InGameScreen;

namespace zzre.game.systems.ui;

public partial class ScrDeck
{
    private DefaultEcs.Entity CreateDeckSlot(
        DefaultEcs.Entity parent,
        components.ui.ElementId id,
        int index
    )
    {
        var entity = World.CreateEntity();
        entity.Set(new components.Parent(parent));
        entity.Set(new components.ui.Slot());
        ref var slot = ref entity.Get<components.ui.Slot>();
        slot.index = index;
        slot.button = preload.CreateButton(entity)
            .With(id)
            .With(Rect.FromTopLeftSize(Mid + new Vector2(31, 60 + 79 * slot.index), new Vector2(40, 40)))
            .With(new components.ui.ButtonTiles(-1))
            .With(UIPreloadAsset.Wiz000)
            .Build();
        slot.button.Set(new components.ui.SlotButton());
        slot.type = components.ui.Slot.Type.DeckSlot;

        CreateSlotSummary(entity, new(48, 4));

        slot.spellSlots = new DefaultEcs.Entity[4];
        for (int i = 0; i < InventoryFairy.SpellSlotCount; i++)
            slot.spellSlots[i] = CreateSpellSlot(entity, ref slot, i);

        return entity;
    }

    private void SetDeckSlot(DefaultEcs.Entity slotEntity, ref components.ui.ScrDeck deck)
    {
        ref var slot = ref slotEntity.Get<components.ui.Slot>();
        var card = inventory.GetFairyAtSlot(slot.index);
        if (card == default)
        {
            UnsetDeckSlot(ref deck, ref slot);
            return;
        }

        slot.card = card;
        slot.button.Set(components.Visibility.Visible);
        slot.button.Set(new components.ui.ButtonTiles(card.cardId.EntityId));
        SetDeckSlotTooltip(ref deck, ref slot);
        slot.summary.Set(new components.ui.Label(FormatSlotSummary(card)));
        SetSummaryOffset(ref slot);

        for (int i = 0; i < InventoryFairy.SpellSlotCount; i++)
        {
            var spell = slot.card == null
                ? null
                : inventory.GetSpellAtSlot((InventoryFairy)slot.card, i);
            if (spell != default)
                SetSpellSlot(slot.spellSlots[i], spell);
            else
                UnsetSpellSlot(slot.spellSlots[i]);
        }
        if (IsInfoTab(deck.ActiveTab)) InfoMode(ref deck, ref slot);
        else SpellMode(ref deck, ref slot);
    }

    private void UnsetDeckSlot(ref components.ui.ScrDeck deck, ref components.ui.Slot slot)
    {
        slot.card = default;
        slot.button.Set(components.Visibility.Invisible);
        SetDeckSlotTooltip(ref deck, ref slot);
        UnsetSlotSummary(ref slot);
        foreach (var spellSlot in slot.spellSlots)
            UnsetSpellSlot(spellSlot);
    }

    private static void SetDeckSlotTooltip(ref components.ui.ScrDeck deck, ref components.ui.Slot slot)
    {
        var tooltip = DeckCardTooltip(ref deck, ref slot);
        if (tooltip != null)
            slot.button.Set(new components.ui.TooltipUID(tooltip.Value));
        else if (slot.button.Has<components.ui.TooltipUID>())
            slot.button.Remove<components.ui.TooltipUID>();
    }

    private static UID? DeckCardTooltip(ref components.ui.ScrDeck deck, ref components.ui.Slot slot)
    {
        if (deck.DraggedCard == default)
            return slot.card != default
                ? new UID(0x41912581) // select fairy
                : null;

        if (deck.DraggedCard.cardId.Type == CardType.Fairy)
            return slot.card != default
                ? new UID(0x5B971D81) // replace fairy
                : new UID(0xD66A2581); // place fairy

        if (deck.DraggedCard.cardId.Type == CardType.Item)
            return slot.card != default
                ? new UID(0x89A21981) // press key 1
                : new UID(0xD66A2581); // place fairy (?!)

        return null; // no tooltip for dragged spells
    }

    private void InfoMode(ref components.ui.ScrDeck deck, ref components.ui.Slot slot)
    {
        if (slot.card != default)
            slot.summary.Set(new components.ui.Label(FormatSlotSummary((InventoryFairy)(slot.card))));
        foreach (var spellSlot in slot.spellSlots)
            InfoModeSpell(ref deck, ref slot, ref spellSlot.Get<components.ui.Slot>());
    }

    private static void SpellMode(ref components.ui.ScrDeck deck, ref components.ui.Slot slot)
    {
        slot.summary.Set(new components.ui.Label(""));
        foreach (var spellSlot in slot.spellSlots)
            SpellModeSpell(ref deck, ref slot, ref spellSlot.Get<components.ui.Slot>());
    }

    private void HandleDeckSlotClick(DefaultEcs.Entity deckEntity, ref components.ui.ScrDeck deck, DefaultEcs.Entity slotEntity, ref components.ui.Slot slot)
    {
        if (deck.DraggedCard == default)
        {
            // Clicking on an empty slot with an empty hand
            if (slot.card == default)
                return;
            // Picking up a card
            inventory.SetSlot((InventoryFairy)slot.card, -1);
            deck.VacatedDeckSlot = slot.index;
            DragCard(deckEntity, ref deck, slot.card);
            UnsetDeckSlot(ref deck, ref slot);
            SetListSlots(ref deck);
            return;
        }

        // Applying a card
        if (deck.DraggedCard.cardId.Type == CardType.Spell)
            return;
        if (deck.DraggedCard.cardId.Type == CardType.Fairy)
        {
            var oldDrag = deck.DraggedCard;
            var newDrag = slot.card;

            World.Publish(new messages.SpawnSample("resources/audio/sfx/gui/_g012.wav"));
            inventory.SetSlot((InventoryFairy)oldDrag, slot.index);

            // Swap fairies
            if (newDrag != default)
            {
                DragCard(deckEntity, ref deck, newDrag);
                SetListSlots(ref deck);
                if (newDrag.isInUse)
                {
                    inventory.SetSlot((InventoryFairy)newDrag, -1);
                    deck.VacatedDeckSlot = slot.index;
                }
            }
            else
            {
                // Drop off fairy
                deck.VacatedDeckSlot = -1;
                DropCard(ref deck);
            }
            SetDeckSlot(slotEntity, ref deck);
            return;
        }
        else if (deck.DraggedCard.cardId.Type == CardType.Item)
        {
            if (slot.card == default)
                return;
            World.Publish(new messages.ui.ExecuteUIScript((InventoryItem)deck.DraggedCard, slotEntity));
        }
    }

    protected void HandleUIScriptFinished(in messages.ui.UIScriptFinished message)
    {
        ref var deck = ref message.DeckSlotEntity.Get<components.Parent>().Entity.Get<components.ui.ScrDeck>();

        SetDeckSlot(message.DeckSlotEntity, ref deck);
        if (message.ItemConsumed)
        {
            inventory.RemoveCards(deck.DraggedCard!.cardId, 1);
            DropCard(ref deck);
        }
    }
}
