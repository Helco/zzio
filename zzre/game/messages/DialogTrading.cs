using System.Collections.Generic;
using zzio;

namespace zzre.game.messages;

public record struct DialogTrading(
    DefaultEcs.Entity DialogEntity,
    UID CurrencyUID,
    List<(int price, UID uid)> CardTrades
);
