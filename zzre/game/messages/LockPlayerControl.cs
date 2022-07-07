using System;

namespace zzre.game.messages
{
    public record struct LockPlayerControl(float Duration)
    {
        public static readonly LockPlayerControl Unlock = default;
    public static readonly LockPlayerControl Forever = new LockPlayerControl(float.PositiveInfinity);
}
}
