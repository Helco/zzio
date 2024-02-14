namespace zzre.game.systems;
using System;
using System.Linq;
using System.Numerics;
using DefaultEcs.System;
using DefaultEcs.Command;
using zzio.scn;

using WaypointMode = zzre.game.messages.NPCMoveSystem.Mode;

[PauseDuring(PauseTrigger.UIScreen)]
public partial class NPCMovementByState : NPCMovementBase
{
    private const float TargetDistanceToPlayer = 1.1f;
    private const float MaxPlayerDistanceSqr = 81f;
    private const float MaxWaypointDistanceSqr = 49f;
    private const float Mode1Chance = 0.3f;

    private readonly EntityCommandRecorder recorder;
    private readonly IDisposable changeWaypointSubscription;
    private readonly IDisposable moveSystemSubscription;
    private readonly IDisposable unreserveWaypointSubscription;

    public NPCMovementByState(ITagContainer diContainer) : base(diContainer, CreateEntityContainer, useBuffer: false)
    {
        recorder = diContainer.GetTag<EntityCommandRecorder>();
        changeWaypointSubscription = World.Subscribe<messages.NPCChangeWaypoint>(HandleChangeWaypoint);
        moveSystemSubscription = World.Subscribe<messages.NPCMoveSystem>(HandleMoveSystem);
        unreserveWaypointSubscription = World.Subscribe<messages.UnreserveNextWaypoint>(HandleUnreserveNextWaypoint);
    }

    public override void Dispose()
    {
        base.Dispose();
        changeWaypointSubscription.Dispose();
        moveSystemSubscription.Dispose();
        unreserveWaypointSubscription.Dispose();
    }

    [WithPredicate]
    private static bool IsMovementNPCState(in components.NPCState value) => value == components.NPCState.Waypoint;

    private void HandleUnreserveNextWaypoint(in messages.UnreserveNextWaypoint msg)
    {
        ref var move = ref msg.npcEntity.Get<components.NPCMovement>();
        if (move.NextWaypointId >= 0)
            waypointByIdx[move.NextWaypointId].ii3 = 0;
    }

    private void HandleChangeWaypoint(in messages.NPCChangeWaypoint msg)
    {
        var location = msg.Entity.Get<Location>();
        ref var move = ref msg.Entity.Get<components.NPCMovement>();
        if (msg.FromWaypoint != move.CurWaypointId && move.CurWaypointId != -1)
            return;
        move.CurWaypointId = msg.FromWaypoint;
        move.NextWaypointId = msg.ToWaypoint;

        if (msg.ToWaypoint == -1)
        {
            var dirToPlayer = Vector3.Normalize(PlayerLocation.LocalPosition - location.LocalPosition);
            move.TargetPos = PlayerLocation.LocalPosition - dirToPlayer * TargetDistanceToPlayer;
        }
        else
            move.TargetPos = waypointById[msg.ToWaypoint].pos;

        move.DistanceToTarget = Vector3.Distance(location.LocalPosition, move.TargetPos);
        move.DistanceWalked = 0f;
        SwitchToWalkingAnimation(msg.Entity);
    }

    private void HandleMoveSystem(in messages.NPCMoveSystem msg)
    {
        var location = msg.Entity.Get<Location>();
        ref var move = ref msg.Entity.Get<components.NPCMovement>();

        var waypointMode = msg.WaypointMode;
        if (waypointMode == WaypointMode.FarthestFromPlayer &&
            Vector3.DistanceSquared(location.LocalPosition, PlayerLocation.LocalPosition) > MaxPlayerDistanceSqr)
            waypointMode = WaypointMode.LuckyNearest;
        var nextWaypoint = ChooseNextWaypoint(waypointMode, msg.WaypointCategory, location, move);
        if (nextWaypoint == null)
            return;

        if (waypointByIdx.TryGetValue(move.CurWaypointId, out var curWaypointTrigger))
            curWaypointTrigger.ii3 = 0;
        nextWaypoint.ii3 = 1; // reserving this waypoint
        move.NextWaypointId = (int)nextWaypoint.idx;
        move.TargetPos = nextWaypoint.pos;
        move.DistanceToTarget = Vector3.Distance(location.LocalPosition, move.TargetPos);
        move.DistanceWalked = 0f;
        SwitchToWalkingAnimation(msg.Entity);
        msg.Entity.Set(components.NPCState.Waypoint);
    }

    private Trigger? ChooseNextWaypoint(WaypointMode waypointMode, int wpCategory, Location location, in components.NPCMovement move)
    {
        var random = Random.Shared;
        var (lastWaypointId, curWaypointId) = (move.LastWaypointId, move.CurWaypointId);

        switch (waypointMode)
        {
            case WaypointMode.FarthestFromPlayer:
                if (Vector3.DistanceSquared(location.LocalPosition, PlayerLocation.LocalPosition) > MaxPlayerDistanceSqr)
                    return ChooseNextWaypoint(WaypointMode.LuckyNearest, wpCategory, location, move);
                return waypointsByCategory[wpCategory]
                    .Where(wp => wp.ii3 == 0 && wp.idx != curWaypointId)
                    .Where(wp => Vector3.DistanceSquared(location.LocalPosition, wp.pos) < MaxWaypointDistanceSqr)
                    .OrderByDescending(wp => Vector3.DistanceSquared(PlayerLocation.LocalPosition, wp.pos))
                    .FirstOrDefault();

            case WaypointMode.LuckyNearest:
                {
                    var potentialWps = waypointsByCategory[wpCategory]
                        .Where(wp => wp.ii3 == 0 && wp.idx != lastWaypointId && wp.idx != curWaypointId)
                        .OrderBy(wp => Vector3.DistanceSquared(location.LocalPosition, wp.pos));
                    return move.CurWaypointId < 0
                        ? potentialWps.FirstOrDefault()
                        : potentialWps.FirstOrDefault(wp => random.NextFloat() > Mode1Chance);
                }

            case WaypointMode.Random:
                {
                    var lastTargetPos = move.CurWaypointId < 0 ? location.LocalPosition : move.LastTargetPos;
                    var potentialWps = waypointsByCategory[wpCategory]
                        .Where(wp => wp.ii3 == 0 && wp.idx != lastWaypointId && wp.idx != curWaypointId)
                        .Where(wp => Vector3.DistanceSquared(lastTargetPos, wp.pos) < MaxWaypointDistanceSqr)
                        .ToArray();
                    return potentialWps.Any()
                        ? random.NextOf(potentialWps)
                        : null;
                }

            default: throw new NotImplementedException($"Unimplemented waypoint mode {waypointMode}");
        }
    }

    [Update]
    private void Update(
        float elapsedTime,
        in DefaultEcs.Entity entity,
        components.NPCType npcType,
        Location location,
        ref components.NPCMovement move,
        ref components.NonFairyAnimation animation)
    {
        var hasArrived = UpdateWalking(elapsedTime, entity, npcType, location, ref move);

        if (hasArrived)
        {
            if (move.CurWaypointId == -1)
            {
                // TODO: Add missing UI and currentNPC behavior for arriving NPCs 
                animation.Next = zzio.AnimationType.Idle0;
            }
            recorder.Record(entity).Set(components.NPCState.Script);
        }
        else
        {
            SwitchToWalkingAnimation(entity);
            // TODO: Check whether player slerping to walking NPCs is a thing
        }
    }

    private static void SwitchToWalkingAnimation(DefaultEcs.Entity entity)
    {
        // there is one NPC (g008s01m) that only has an idle animation
        // to keep the safeguard we only fallback for Walk0 to Idle0
        // also keep in mind that Fairy NPCs do not have bodies
        if (!entity.TryGet<components.ActorParts>(out var parts))
            return;
        ref readonly var animationPool = ref parts.Body.Get<components.AnimationPool>();
        entity.Get<components.NonFairyAnimation>().Next = animationPool.Contains(zzio.AnimationType.Walk0)
            ? zzio.AnimationType.Walk0
            : zzio.AnimationType.Idle0;
    }
}
