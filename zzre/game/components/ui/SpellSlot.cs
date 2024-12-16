using zzio;

namespace zzre.game.components.ui;

public record struct SpellSlot
{
    public InventorySpell? spell;
    public int index;
    public DefaultEcs.Entity button;
    public DefaultEcs.Entity summary;
    public DefaultEcs.Entity req;
};
