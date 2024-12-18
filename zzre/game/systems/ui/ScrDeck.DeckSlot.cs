using System;
using System.Numerics;
using zzio;
using static zzre.game.systems.ui.InGameScreen;

namespace zzre.game.systems.ui;

public partial class ScrDeck
{
    private static readonly UID UIDSelectFairy = new(0x41912581);
    private static readonly UID UIDPlaceFairy = new(0x41912581);
    private static readonly UID UIDReplaceFairy = new(0x41912581);
    private static readonly UID UIDUseItemOnFairy = new(0x41912581);

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
        slot.buttonId = id;
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
            UnsetSlot(ref slot);
            return;
        }

        slot.card = card;
        slot.button.Set(components.Visibility.Visible);
        slot.button.Set(new components.ui.ButtonTiles(card.cardId.EntityId));
        slot.button.Set(new components.ui.TooltipUID(UIDSelectFairy));
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
        if (IsInfoTab(deck.ActiveTab)) InfoMode(ref slot);
        else SpellMode(ref slot);
    }

    private void InfoMode(ref components.ui.Slot slot)
    {
        if (slot.card != default)
            slot.summary.Set(new components.ui.Label(FormatSlotSummary((InventoryFairy)(slot.card))));
        foreach (var spellSlot in slot.spellSlots)
            InfoModeSpell(ref spellSlot.Get<components.ui.Slot>());
    }

    private static void SpellMode(ref components.ui.Slot slot)
    {
        slot.summary.Set(new components.ui.Label(""));
        foreach (var spellSlot in slot.spellSlots)
            SpellModeSpell(ref spellSlot.Get<components.ui.Slot>());
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
            UnsetSlot(ref slot);
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
            Console.WriteLine("Apply item {deck.DraggedCard.cardId} to fairy {slotEntity}");
        }
    }
}
