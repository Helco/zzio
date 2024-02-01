using System.Collections.Generic;
using zzio;

namespace zzre.game.messages;

public record struct DialogGambling(
    DefaultEcs.Entity DialogEntity,
    List<int> Cards
);

