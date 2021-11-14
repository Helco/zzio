using System;
using System.Numerics;

namespace zzre.game.components
{
    public struct NPCMovement
    {
        public float Speed;
        public int LastWaypointId;
        public int CurWaypointId;
        public int NextWaypointId;

        public Vector3 TargetPos;
        public Vector3 LastTargetPos;
        public float DistanceToTarget;
        public float DistanceWalked;

        public static readonly NPCMovement Default = new NPCMovement()
        {
            Speed = 1.5f,
            LastWaypointId = -1,
            CurWaypointId = -1,
            NextWaypointId = -1
        };
    }
}
