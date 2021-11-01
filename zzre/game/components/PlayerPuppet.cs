using System;
using System.Numerics;

namespace zzre.game.components
{
    public struct PlayerPuppet
    {
        public bool DidResetPlanarVelocity; // after switching to idling
        public float FallTimer;
        public float FallAnimationTimer;
        public zzio.AnimationType NextAnimation;
    }
}
