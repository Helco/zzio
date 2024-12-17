using zzio;

namespace zzre.game.components.ui;

public record struct Slot
{
    public enum Type { DeckSlot, ListSlot, SpellSlot }
    public Type type;
    public int index;
    public InventoryCard? card;
    public DefaultEcs.Entity button;
    public components.ui.ElementId buttonId;
    public DefaultEcs.Entity usedMarker; // ListSlot only
    public DefaultEcs.Entity summary;
    public DefaultEcs.Entity req; // SpellSlot only
    public DefaultEcs.Entity[] spellSlots; // DeckSlot only
};
