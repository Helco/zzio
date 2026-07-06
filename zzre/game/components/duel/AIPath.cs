using System.Diagnostics;
using System.Numerics;

namespace zzre.game.components;

public struct AIPath
{
    public PooledList<uint> WaypointIds;
    public PooledList<Vector3> Waypoints;
    public PooledList<WaypointEdgeKind> EdgeKinds;
    public FindPathResult LastResult;

    public int TargetIndex
    {
        get;
        set
        {
            Debug.Assert(IsInBounds(value));
            field = value;
        }
    }

    public readonly bool IsInBounds(int index) => Waypoints.IsEmpty || (index >= 0 && index < Waypoints.Count);
    public readonly bool HasNextWaypoint => TargetIndex + 1 < WaypointIds.Count;
    public readonly bool HasPrevWaypoint => TargetIndex > 0;
}
