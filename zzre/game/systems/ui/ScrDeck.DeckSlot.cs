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

    private DefaultEcs.Entity CreateDeckSlot(
    DefaultEcs.Entity parent,
    Vector2 pos,
    components.ui.ElementId id
    )
    {
        var entity = CreateBaseSlot(parent, pos, id);
        ref var slot = ref entity.Get<components.ui.Slot>();

        CreateSlotSummary(entity, new(48, 4));

        slot.spellSlots = new DefaultEcs.Entity[4];
        for (int i = 0; i < InventoryFairy.SpellSlotCount; i++)
            slot.spellSlots[i] = CreateSpellSlot(entity, ref slot, i);

        return entity;
    }

    private void SetDeckSlot(DefaultEcs.Entity entity, InventoryCard card)
    {
        ref var slot = ref entity.Get<components.ui.Slot>();
        SetSlot(ref slot, card);
        slot.button.Set(new components.ui.TooltipUID(UIDSelectFairy));
    }

    private void InfoMode(ref components.ui.Slot slot)
    {
        if (slot.card != default)
            slot.summary.Set(new components.ui.Label(slot.card switch
            {
                InventoryItem item => FormatSlotSummary(item),
                InventorySpell spell => FormatSlotSummary(spell),
                InventoryFairy fairy => FormatSlotSummary(fairy),
                _ => throw new NotSupportedException("Unknown inventory card type")
            }));
        foreach (var spellSlot in slot.spellSlots)
            InfoMode(ref spellSlot.Get<components.ui.SpellSlot>());
    }

    private static void SpellMode(ref components.ui.Slot slot)
    {
        slot.summary.Set(new components.ui.Label(""));
        foreach (var spellSlot in slot.spellSlots)
            SpellMode(ref spellSlot.Get<components.ui.SpellSlot>());
    }
}
