using System;

namespace zzre.game
{
    public enum DialogCause
    {
        Trigger,
        PlayerWon,
        PlayerCaught,
        NpcFled,
        PlayerLost,
        PlayerFled
    }
}

namespace zzre.game.messages
{
    public record struct StartDialog(DefaultEcs.Entity NpcEntity, DialogCause Cause, int CatchItemId = -1);
}
