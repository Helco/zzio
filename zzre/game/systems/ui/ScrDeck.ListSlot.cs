using System;
using System.Numerics;
using zzio;

namespace zzre.game.systems.ui;

public partial class ScrDeck
{

    private DefaultEcs.Entity CreateListSlot(
        DefaultEcs.Entity parent,
        Vector2 pos,
        components.ui.ElementId id,
        int index
        )
    {
        var entity = World.CreateEntity();
        entity.Set(new components.Parent(parent));
        entity.Set(new components.ui.Slot());
        ref var slot = ref entity.Get<components.ui.Slot>();
        slot.type = components.ui.Slot.Type.ListSlot;
        slot.index = index;
        slot.button = preload.CreateButton(entity)
            .With(id)
            .With(pos)
            .With(new components.ui.ButtonTiles(-1))
            .With(UIPreloadAsset.Wiz000)
            .Build();
        slot.button.Set(new components.ui.SlotButton());

        slot.usedMarker = preload.CreateImage(entity)
            .With(pos)
            .With(UIPreloadAsset.Inf000, 16)
            .WithRenderOrder(-1)
            .Invisible()
            .Build();

        UnsetSlot(ref slot);
        return entity;
    }

    private void SetListSlot(DefaultEcs.Entity entity, InventoryCard card)
    {
        ref var slot = ref entity.Get<components.ui.Slot>();
        SetSlot(ref slot, card);
        slot.usedMarker.Set(!IsDraggable(card)
            ? components.Visibility.Visible
            : components.Visibility.Invisible);
        slot.button.Set(CardTooltip(card));

        if (slot.spellImages != default)
            if (slot.showSpells)
                SetSpellImages(entity);
            else
                UnsetSpellImages(ref slot);
    }

    private bool IsDraggable(InventoryCard card) => card.cardId.Type switch
    {
        CardType.Item => mappedDB.GetItem(card.dbUID).Script.Length != 0,
        CardType.Spell => !card.isInUse,
        CardType.Fairy => !card.isInUse,
        _ => throw new ArgumentException($"Invalid inventory card type: {card.cardId.Type}")
    };

    private void HandleListSlotClick(DefaultEcs.Entity deckEntity, ref components.ui.ScrDeck deck, DefaultEcs.Entity slotEntity, ref components.ui.Slot slot)
    {
        if (deck.DraggedCard != default) return;
        if (slot.card == default) return;
        if (!IsDraggable(slot.card)) return;
        if (!scene.dataset.canChangeDeck && slot.card.cardId.Type == CardType.Fairy)
        {
            ui.Publish(new messages.ui.Notification(mappedDB.GetText(new UID(0xC21C5531)).Text));
            return;
        }
        DragCard(deckEntity, ref deck, slot.card);
    }

    private void CreateSpellImages(DefaultEcs.Entity entity)
    {
        ref var slot = ref entity.Get<components.ui.Slot>();
        slot.spellImages = new DefaultEcs.Entity[4];
        for (var i = 0; i < slot.spellImages.Length; i++)
        {
            slot.spellImages[i] = preload.CreateImage(entity)
                .With(slot.button.Get<Rect>().Min + new Vector2(42 + 40 * i, 0))
                .With(UIPreloadAsset.Spl000)
                .Build();
        }
        UnsetSpellImages(ref slot);
    }

    private void SetHoverMode(DefaultEcs.Entity entity)
    {
        ref var slot = ref entity.Get<components.ui.Slot>();
        if (slot.summary == default) return;
        slot.showSpells = true;
        SetSpellImages(entity);
    }
    private void UnsetHoverMode(DefaultEcs.Entity entity)
    {
        ref var slot = ref entity.Get<components.ui.Slot>();
        if (slot.summary == default) return;
        slot.showSpells = false;
        UnsetSpellImages(ref slot);
    }

    private void SetSpellImages(DefaultEcs.Entity entity)
    {
        ref var slot = ref entity.Get<components.ui.Slot>();
        if (slot.card == default || slot.card.cardId.Type != CardType.Fairy)
        {
            UnsetSpellImages(ref slot);
            return;
        }
        for (var i = 0; i < slot.spellImages.Length; i++)
        {
            var spell = inventory.GetSpellAtSlot((InventoryFairy)slot.card, i);
            if (spell != default)
            {
                slot.spellImages[i].Set(components.Visibility.Visible);
                slot.spellImages[i].Set(new components.ui.ButtonTiles(spell.cardId.EntityId));
            }
            else UnsetSpellImage(slot.spellImages[i]);
        }
        UnsetSlotSummary(ref slot);
    }

    private void UnsetSpellImages(ref components.ui.Slot slot)
    {
        foreach (var spellImage in slot.spellImages)
            UnsetSpellImage(spellImage);
        SetSlotSummary(ref slot);
    }

    private static void UnsetSpellImage(DefaultEcs.Entity spellImage)
    {
        spellImage.Set(components.Visibility.Invisible);
        spellImage.Set(new components.ui.ButtonTiles(-1));
    }

    private components.ui.TooltipUID CardTooltip(InventoryItem item)
            => !IsDraggable(item)
            ? new UID(0x8F4510A1) // item cannot be used
            : new UID(0x75F10CA1); // select item

    private components.ui.TooltipUID CardTooltip(InventoryFairy fairy)
        => !IsDraggable(fairy)
        ? new UID(0x9054EAB1) // fairy is in use
        : scene.dataset.canChangeDeck
        ? new UID(0x00B500A1) // select fairy
        : new UID(0x4D1B04A1); // can only be changed in London

    private components.ui.TooltipUID CardTooltip(InventorySpell spell)
        => !IsDraggable(spell)
        ? new UID(0x6B46EEB1) // spell is in use
        : mappedDB.GetSpell(spell.dbUID).Type == 0
        ? new UID(0xDA2B08A1) // select offensive spell
        : new UID(0x93840CA1); // select passive spell

    private components.ui.TooltipUID CardTooltip(InventoryCard card) => card.cardId.Type switch
    {
        CardType.Item => CardTooltip((InventoryItem)card),
        CardType.Spell => CardTooltip((InventorySpell)card),
        CardType.Fairy => CardTooltip((InventoryFairy)card),
        _ => throw new ArgumentException($"Invalid inventory card type: {card.cardId.Type}")
    };
}
