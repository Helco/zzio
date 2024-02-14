using System;

namespace zzre.game.messages;

public record struct LockPlayerControl(float Duration, bool MovingForward = false)
{
    public static readonly LockPlayerControl Unlock;
    public static readonly LockPlayerControl Forever = new(float.PositiveInfinity);
}
