using System;
using System.Numerics;

namespace zzre.game.components
{
    public struct PlayerPuppet
    {
        public float FallTimer;
        public bool DidResetPlanarVelocity; // after switching to idling
    }
}
