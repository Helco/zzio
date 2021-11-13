using System;

namespace zzre.game.components
{
    public enum NPCState
    {
        Script,
        Waypoint,
        SmoothLookAtPlayer,
        HardLookAtPlayer,
        BillboardLookAtPlayer,
        LookAtTrigger,
        BreakableAnimation,
        Idle
    }
}
