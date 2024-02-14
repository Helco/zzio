using System.Collections.Generic;

namespace zzre.game.components.ui;

public struct ScrRuneMenu
{
    public Inventory Inventory;
    public IReadOnlyList<DefaultEcs.Entity> RuneButtons;
    public DefaultEcs.Entity LastHoveredRune;
}
