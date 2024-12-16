using System;
using System.Numerics;
using zzio;

namespace zzre.game.systems.ui;

public partial class ScrDeck
{
    private static readonly UID UIDSelectFairy = new(0x41912581);
    private static readonly UID UIDPlaceFairy = new(0x41912581);
    private static readonly UID UIDReplaceFairy = new(0x41912581);
    private static readonly UID UIDUseItemOnFairy = new(0x41912581);

    private DefaultEcs.Entity CreateDeckCard(
    DefaultEcs.Entity parent,
    Vector2 pos,
    components.ui.ElementId id
    )
    {
        var entity = CreateBaseCard(parent, pos, id);
        ref var card = ref entity.Get<components.ui.Card>();

        CreateCardSummary(entity, new(48, 4));

        card.spellSlots = new DefaultEcs.Entity[4];
        for (int i = 0; i < InventoryFairy.SpellSlotCount; i++)
            card.spellSlots[i] = CreateSpellSlot(entity, ref card, i);

        return entity;
    }

    private void SetDeckCard(DefaultEcs.Entity entity, InventoryCard invCard)
    {
        ref var card = ref entity.Get<components.ui.Card>();
        SetCard(ref card, invCard);
        card.button.Set(new components.ui.TooltipUID(UIDSelectFairy));
    }

    private void InfoMode(ref components.ui.Card card)
    {
        if (card.card != default)
            card.summary.Set(new components.ui.Label(card.card switch
            {
                InventoryItem item => FormatSummary(item),
                InventorySpell spell => FormatSummary(spell),
                InventoryFairy fairy => FormatSummary(fairy),
                _ => throw new NotSupportedException("Unknown inventory card type")
            }));
        foreach (var slot in card.spellSlots)
            InfoMode(ref slot.Get<components.ui.SpellSlot>());
    }

    private static void SpellMode(ref components.ui.Card card)
    {
        card.summary.Set(new components.ui.Label(""));
        foreach (var slot in card.spellSlots)
            SpellMode(ref slot.Get<components.ui.SpellSlot>());
    }

}
