using System;
using System.Collections.Generic;
using zzio;
using zzio.db;

namespace zzre.game.components.ui;

public record struct DialogTrading(
    DefaultEcs.Entity DialogEntity,
    ItemRow Currency,
    List<(int price, UID uid)> CardTrades,
    Dictionary<components.ui.ElementId, ItemRow> CardPurchaseButtons
)
{
    public DefaultEcs.Entity Primary;
}
