using zzio;

namespace zzre.game.components.ui;

public record struct Slot
{
    public enum Type { DeckSlot, ListSlot }
    public Type type;
    public InventoryCard? card;
    public DefaultEcs.Entity button;
    public components.ui.ElementId buttonId;
    public DefaultEcs.Entity usedMarker;
    public DefaultEcs.Entity summary;
    public DefaultEcs.Entity[] spellSlots;
};
