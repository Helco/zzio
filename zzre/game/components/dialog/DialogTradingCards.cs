using System;
using System.Collections.Generic;
using zzio;

namespace zzre.game.components.ui;

public record struct DialogTradingCards(DefaultEcs.Entity DialogEntity)
{
    public List<(int price, UID uid)> cardTrades;
}
