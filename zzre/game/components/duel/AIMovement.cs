using System.Numerics;

namespace zzre.game.components;

public struct AIMovement
{
    public Vector3 CurrentPos;
    public Vector3 TargetTargetDir;
    public Vector3 DirToCurrentWp;
    public WaypointEdgeKind CurrentEdgeKind;
    public float YVelocity;
    public float DistToCurWp;
    public float DistMovedToCurWp;
    public bool ShouldJump;
    public bool DidMove;
    public bool TryBailout;
    public bool ShouldAdvanceNode;
    public bool DidTimeoutFindingPath;
}
