using zzio;

namespace zzre.game.components.ui;

public record struct Card
{
    public enum Type { DeckCard, ListCard }
    public Type type;
    public InventoryCard? card;
    public DefaultEcs.Entity button;
    public components.ui.ElementId buttonId;
    public DefaultEcs.Entity usedMarker;
    public DefaultEcs.Entity summary;
    public DefaultEcs.Entity[] spellSlots;
};
