using System;

namespace zzre.game.components
{
    public struct NPC
    {
        public enum State
        {
            Normal,
            Waypoint,
            SmoothLookAtPlayer,
            HardLookAtPlayer,
            BillboardLookAtPlayer,
            LookAtTrigger,
            BreakableAnimation,
            Idle
        }

        public State CurrentState;
        public float StateTimer;
    }
}
