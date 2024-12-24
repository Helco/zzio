using System.Numerics;
using System.Linq;
using zzio;

namespace zzre.game.systems.ui;

public partial class ScrDeck
{
    public Vector2 SpellSlotSize = new(38, 38);
    private static readonly UID[] UIDSpellSlotNames =
    [
        new(0x37697321), // First offensive slot
        new(0x8A717321), // First defensive slot
        new(0x0F207721), // Second offensive slot
        new(0x5C577721)  // Second defensive slot
    ];

    private static readonly UID[] UIDFairyInfoDescriptions =
    [
        new(0x45B032A1), // Current and max HP
        new(0xE58236A1), // Level of your fairy
        new(0xB26B36A1), // XP, current and necessary for next level
        new(0xB26B36A1)
    ];

    private DefaultEcs.Entity CreateSpellSlot(
        DefaultEcs.Entity parent,
        ref components.ui.Slot parentSlot,
        int index
    )
    {
        var entity = World.CreateEntity();
        entity.Set(new components.Parent(parent));
        entity.Set(new components.ui.Slot());
        ref var slot = ref entity.Get<components.ui.Slot>();
        slot.type = components.ui.Slot.Type.SpellSlot;
        slot.index = index;
        slot.button = preload.CreateButton(entity)
            .With(parentSlot.button.Get<components.ui.ElementId>() + index + 1)
            .With(Rect.FromTopLeftSize(parentSlot.button.Get<Rect>().Min + new Vector2(50 + 46 * slot.index, 0), SpellSlotSize))
            .With(new components.ui.ButtonTiles(-1))
            .With(UIPreloadAsset.Spl000)
            .Build();
        slot.button.Set(new components.ui.SlotButton());

        InfoModeSpell(ref parent.Get<components.Parent>().Entity.Get<components.ui.ScrDeck>(), ref parentSlot, ref slot);

        return entity;
    }

    public void SetSpellSlot(DefaultEcs.Entity entity, InventorySpell invSpell)
    {
        ref var slot = ref entity.Get<components.ui.Slot>();
        slot.card = invSpell;
        slot.button.Set(components.Visibility.Visible);
        slot.button.Set(new components.ui.ButtonTiles(invSpell.cardId.EntityId));
        CreateSpellSummary(entity);
        CreateSpellReq(entity);
    }

    public void UnsetSpellSlot(DefaultEcs.Entity entity)
    {
        ref var slot = ref entity.Get<components.ui.Slot>();
        UnsetSlot(ref slot);
        slot.button.Set(new components.ui.ButtonTiles(-1));
        CreateSpellSummary(entity);
        CreateSpellReq(entity);
    }

    private static void SetSpellSlotTooltip(ref components.ui.ScrDeck deck, ref components.ui.Slot fairySlot, ref components.ui.Slot slot)
    {
        var tooltip = SpellSlotTooltip(ref deck, ref fairySlot, ref slot);
        if (tooltip != null)
            slot.button.Set(new components.ui.TooltipUID(tooltip.Value));
        else if (slot.button.Has<components.ui.TooltipUID>())
            slot.button.Remove<components.ui.TooltipUID>();
    }

    private static UID? SpellSlotTooltip(ref components.ui.ScrDeck deck, ref components.ui.Slot fairySlot, ref components.ui.Slot slot)
    {
        if (fairySlot.card == default) return null;
        if (deck.DraggedCard == default)
            return IsInfoTab(deck.ActiveTab)
                ? UIDFairyInfoDescriptions[slot.index]
                : UIDSpellSlotNames[slot.index];
        if (deck.DraggedCard.cardId.Type == CardType.Spell)
            return slot.card == default
                ? new UID(0x3B46CC81) // deploy spell
                : new UID(0x0A05D081); // overwrite spell
        return null;
    }

    public void InfoModeSpell(ref components.ui.ScrDeck deck, ref components.ui.Slot fairySlot, ref components.ui.Slot slot)
    {
        slot.button.Set(components.Visibility.Invisible);
        SetSpellSlotTooltip(ref deck, ref fairySlot, ref slot);
        if (slot.summary != default)
            slot.summary.Set(slot.card != default
                ? new components.ui.Label(FormatManaAmount((InventorySpell)slot.card))
                : new components.ui.Label(""));
        if (slot.req != default)
            slot.req.Set(components.Visibility.Invisible);
    }

    public static void SpellModeSpell(ref components.ui.ScrDeck deck, ref components.ui.Slot fairySlot, ref components.ui.Slot slot)
    {
        if (slot.button.Get<components.ui.ButtonTiles>().Normal != -1)
            slot.button.Set(components.Visibility.Visible);
        SetSpellSlotTooltip(ref deck, ref fairySlot, ref slot);
        if (slot.summary != default)
            slot.summary.Set(new components.ui.Label(""));
        if (slot.req != default)
            slot.req.Set(components.Visibility.Visible);
    }

    private void HandleSpellSlotClick(DefaultEcs.Entity deckEntity, ref components.ui.ScrDeck deck, DefaultEcs.Entity slotEntity, ref components.ui.Slot slot)
    {
        if (deck.DraggedCard == default) return;
        if (deck.DraggedCard.cardId.Type != CardType.Spell) return;

        var fairyEntity = slotEntity.Get<components.Parent>().Entity;
        ref var fairySlot = ref fairyEntity.Get<components.ui.Slot>();

        if (fairySlot.card == default) return;

        if (!IsOfMatchingSpellType(ref slot, deck.DraggedCard))
        {
            var note = slot.index % 2 == 0 ? new UID(0xC18D4C31) : new UID(0x9CD74C31);
            ui.Publish(new messages.ui.Notification(mappedDB.GetText(note).Text));
            return;
        }
        if (!((InventoryFairy)fairySlot.card).spellReqs[slot.index].IsCompatible(mappedDB.GetSpell(((InventorySpell)deck.DraggedCard).dbUID)))
        {
            // Not enough skills to use spell
            ui.Publish(new messages.ui.Notification(mappedDB.GetText(new UID(0x79C75031)).Text));
            return;
        }

        var oldDrag = deck.DraggedCard;
        var newDrag = slot.card;

        World.Publish(new messages.SpawnSample("resources/audio/sfx/gui/_g008.wav"));
        inventory.SetSpellSlot((InventoryFairy)fairySlot.card, (InventorySpell)oldDrag, slot.index);

        // Swap spells
        if (newDrag != default)
        {
            newDrag.isInUse = false; // Is this never handled in Inventory.GameLogic?
            DragCard(deckEntity, ref deck, newDrag);
            SetListSlots(ref deck);
        }
        else
        {
            // Drop off spell
            DropCard(ref deck);
        }
        // Update the slot visual
        SetDeckSlot(fairyEntity, ref deck);
        return;
    }

    private bool IsOfMatchingSpellType(ref components.ui.Slot slot, InventoryCard card) =>
        mappedDB.GetSpell(card.dbUID).Type == slot.index % 2;

    private void CreateSpellSummary(DefaultEcs.Entity entity)
    {
        ref var spellSlot = ref entity.Get<components.ui.Slot>();
        if (spellSlot.summary != default) spellSlot.summary.Dispose();
        spellSlot.summary = preload.CreateLabel(entity)
            .With(spellSlot.button.Get<Rect>().Min + new Vector2(0, 48))
            .With(UIPreloadAsset.Fnt002)
            .Build();
    }

    private void CreateSpellReq(DefaultEcs.Entity entity)
    {
        ref var slot = ref entity.Get<components.ui.Slot>();
        if (slot.req != default)
        {
            slot.req.Dispose();
            slot.req = default;
        };

        ref var deckSlot = ref entity.Get<components.Parent>().Entity.Get<components.ui.Slot>();
        if (deckSlot.card == default) return;

        var spellReq = ((InventoryFairy)deckSlot.card).spellReqs[slot.index];
        var isAttack = slot.index % 2 == 0;
        var pos = slot.button.Get<Rect>().Min + new Vector2(2, 45);

        slot.req = World.CreateEntity();
        slot.req.Set(new components.Parent(entity));
        slot.req.Set(components.Visibility.Visible);
        slot.req.Set(components.ui.UIOffset.Center);
        slot.req.Set(new components.ui.RenderOrder(0));
        slot.req.Set(IColor.White);
        assetRegistry.LoadUITileSheet(slot.req, isAttack ? UIPreloadAsset.Cls001 : UIPreloadAsset.Cls000);

        var tileSize = slot.req.Get<rendering.TileSheet>().GetPixelSize(0);
        slot.req.Set(Rect.FromTopLeftSize(pos, tileSize * 3));
        slot.req.Set(spellReq.Select((req, i) => new components.ui.Tile(
            TileId: (int)req,
            Rect: Rect.FromTopLeftSize(pos + i * new Vector2(8, 5), tileSize)))
            .ToArray());
    }

    private string FormatManaAmount(InventorySpell spell)
    {
        var dbSpell = mappedDB.GetSpell(spell.dbUID);
        return dbSpell.Mana == 5
            ? "{104}-"
            : $"{{104}}{spell.mana}/{dbSpell.MaxMana}";
    }
}
