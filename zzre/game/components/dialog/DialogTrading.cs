using System;
using System.Collections.Generic;
using zzio.db;

namespace zzre.game.components.ui;

public record struct DialogTrading(DefaultEcs.Entity DialogEntity)
{
    public DefaultEcs.Entity Topbar;
    public int TradingCards;
    public Dictionary<components.ui.ElementId, ItemRow> CardPurchaseButtons;
}
