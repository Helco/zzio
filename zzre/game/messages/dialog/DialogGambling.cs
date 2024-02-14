using System.Collections.Generic;

namespace zzre.game.messages;

public record struct DialogGambling(
    DefaultEcs.Entity DialogEntity,
    List<int?> Cards
);

