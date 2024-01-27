using System;
using System.Collections.Generic;
using zzio.db;

namespace zzre.game.components.ui;

public struct ScrBookMenu
{
    public Inventory Inventory;
    public FairyRow[] Fairies;
    public Dictionary<components.ui.ElementId, FairyRow> FairyButtons;
    public DefaultEcs.Entity Sidebar;
    public DefaultEcs.Entity Crosshair;
}
