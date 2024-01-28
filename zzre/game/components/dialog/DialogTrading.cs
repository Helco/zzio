using System;

namespace zzre.game.components.ui;

public record struct DialogTrading(DefaultEcs.Entity DialogEntity)
{
    public DefaultEcs.Entity Topbar;
    public int TradingCards;
}
