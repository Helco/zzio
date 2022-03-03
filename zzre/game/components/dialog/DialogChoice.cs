using System;

namespace zzre.game.components
{
    public record struct DialogChoice(DefaultEcs.Entity DialogEntity, int[] Labels);
}
