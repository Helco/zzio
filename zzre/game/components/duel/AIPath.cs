using System.Numerics;

namespace zzre.game.components;

public struct AIPath
{
    public PooledList<uint> WaypointIds;
    public PooledList<Vector3> Waypoints;
    public PooledList<WaypointEdgeKind> EdgeKinds;
    public int CurrentIndex;
    public FindPathResult LastResult;

    public readonly bool HasPath => CurrentIndex + 1 < WaypointIds.Count;
}

/*

(STATE)

AI PUPPET UPDATE
==============================================
calc speed

isPlayerNear
  ? updateReverse until path no found (reset isPlayerNear if NoFound)
  : updateNormal
earlyExit if not success/NotThereYet

augment targetPos for upward movement
modify jumpPower, sounds, velocities for jumping

update targetDir based on state and spinning
update actor rotation

MOVEMENT NORMAL
==============================================
reset if didReversePath
move with moveDistLeft
result
  move again
  targetPos shift. // main due to NotThereYet 

MOVEMENT LOOP
  early-exit if has path but does not reach next waypoint (NotThereYet)
  advance nextNode if necessary
  needsNewPath if reached end of path or nodeCount < 4?
  bailout behavior (METHOD) affecting needsNewPath
  find new path if needsNewPath with early exit for timeout/NoFound
  setup next wp
  
SETUP NEXT WP
  calc distance to cur wp
  set position to cur wp
  reduce moveDistLeft by dist to cur wp
  switch waypoint
  update curEdgeType
  update distToCurWp
  IF any distance to nextWp
    update dirToCurrentWp
  ELSE
    reset next and prev to path.start.next
    update prevRealWp
    earlyExit if moveDistLeft == 0

MOVEMENT REVERSE
==============================================
if not didReversePath
  invert dirToCurrentWp
  distMovedToCurWp is distance to next wp
  shift nextNode one back
move with moveDistLeft
shouldAdvanceNode if Success/Timeout/NotThereYet (so not NoFound/Invalid)
if Success/NotThereYet/Invalid

... not necessary for first implementations now is it?

*/
