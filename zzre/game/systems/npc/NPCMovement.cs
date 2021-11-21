using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using DefaultEcs.System;
using zzio.scn;

using WaypointMode = zzre.game.messages.NPCMoveSystem.Mode;

namespace zzre.game.systems
{
    public partial class NPCMovement : AEntitySetSystem<float>
    {
        private const float MinSlerpDistance = 0.5f;
        private const float SlerpCurvature = 100f;
        private const float SlerpSpeed = 2f;
        private const float TargetDistanceToPlayer = 1.1f;
        private const float MaxPlayerDistanceSqr = 81f;
        private const float MaxWaypointDistanceSqr = 49f;
        private const float Mode1Chance = 0.3f;

        private Location playerLocation => playerLocationLazy.Value;
        private readonly Lazy<Location> playerLocationLazy;
        private readonly IDisposable sceneLoadedSubscription;
        private readonly IDisposable changeWaypointSubscription;
        private readonly IDisposable moveSystemSubscription;
        private readonly WorldCollider worldCollider;
        private readonly Scene scene;
        private IReadOnlyDictionary<int, Trigger> waypointById = new Dictionary<int, Trigger>();
        private IReadOnlyDictionary<int, Trigger> waypointByIdx = new Dictionary<int, Trigger>();
        private ILookup<int, Trigger> waypointsByCategory = Enumerable.Empty<Trigger>().ToLookup(t => 0);

        public NPCMovement(ITagContainer diContainer) : base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, useBuffer: true)
        {
            var game = diContainer.GetTag<Game>();
            playerLocationLazy = new Lazy<Location>(() => game.PlayerEntity.Get<Location>());
            worldCollider = diContainer.GetTag<WorldCollider>();
            scene = diContainer.GetTag<Scene>();
            sceneLoadedSubscription = World.Subscribe<messages.SceneLoaded>(HandleSceneLoaded);
            changeWaypointSubscription = World.Subscribe<messages.NPCChangeWaypoint>(HandleChangeWaypoint);
            moveSystemSubscription = World.Subscribe<messages.NPCMoveSystem>(HandleMoveSystem);
        }

        public override void Dispose()
        {
            base.Dispose();
            sceneLoadedSubscription.Dispose();
            changeWaypointSubscription.Dispose();
            moveSystemSubscription.Dispose();
        }

        [WithPredicate]
        private static bool IsMovementNPCState(in components.NPCState value) => value == components.NPCState.Waypoint;
        
        private void HandleSceneLoaded(in messages.SceneLoaded _)
        {
            var waypoints = scene.triggers.Where(t => t.type == TriggerType.Waypoint).ToArray();
            waypointById = waypoints
                .GroupBy(wp => (int)wp.ii1)
                .ToDictionary(group => group.Key, group => group.First());
            waypointByIdx = waypoints.ToDictionary(wp => (int)wp.idx, wp => wp);
            waypointsByCategory = waypoints.ToLookup(wp => (int)wp.ii2);
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
                var dirToPlayer = Vector3.Normalize(playerLocation.LocalPosition - location.LocalPosition);
                move.TargetPos = playerLocation.LocalPosition - dirToPlayer * TargetDistanceToPlayer;
            }
            else
                move.TargetPos = waypointById[msg.ToWaypoint].pos;

            move.DistanceToTarget = Vector3.Distance(location.LocalPosition, move.TargetPos);
            move.DistanceWalked = 0f;
            msg.Entity.Get<components.NonFairyAnimation>().Next = zzio.AnimationType.Walk0;
            msg.Entity.Set(components.NPCState.Waypoint);
        }

        private void HandleMoveSystem(in messages.NPCMoveSystem msg)
        {
            var location = msg.Entity.Get<Location>();
            ref var move = ref msg.Entity.Get<components.NPCMovement>();

            var waypointMode = msg.WaypointMode;
            if (waypointMode == WaypointMode.FarthestFromPlayer &&
                Vector3.DistanceSquared(location.LocalPosition, playerLocation.LocalPosition) > MaxPlayerDistanceSqr)
                waypointMode = WaypointMode.LuckyNearest;
            var nextWaypoint = ChooseNextWaypoint(waypointMode, msg.WaypointCategory, location, move);
            if (nextWaypoint == null)
                return;

            if (move.CurWaypointId >= 0)
                waypointByIdx[move.CurWaypointId].ii3 = 0;
            nextWaypoint.ii3 = 1; // reserving this waypoint
            move.NextWaypointId = (int)nextWaypoint.idx;
            move.TargetPos = nextWaypoint.pos;
            move.DistanceToTarget = Vector3.Distance(location.LocalPosition, move.TargetPos);
            move.DistanceWalked = 0f;
            msg.Entity.Get<components.NonFairyAnimation>().Next = zzio.AnimationType.Walk0;
            msg.Entity.Set(components.NPCState.Waypoint);
        }

        private Trigger? ChooseNextWaypoint(WaypointMode waypointMode, int wpCategory, Location location, in components.NPCMovement move)
        {
            var random = GlobalRandom.Get;
            var (lastWaypointId, curWaypointId) = (move.LastWaypointId, move.CurWaypointId);

            switch(waypointMode)
            {
                case WaypointMode.FarthestFromPlayer:
                    if (Vector3.DistanceSquared(location.LocalPosition, playerLocation.LocalPosition) > MaxPlayerDistanceSqr)
                        return ChooseNextWaypoint(WaypointMode.LuckyNearest, wpCategory, location, move);
                    return waypointsByCategory[wpCategory]
                        .Where(wp => wp.ii3 == 0 && wp.idx != curWaypointId)
                        .Where(wp => Vector3.DistanceSquared(location.LocalPosition, wp.pos) < MaxWaypointDistanceSqr)
                        .OrderByDescending(wp => Vector3.DistanceSquared(playerLocation.LocalPosition, wp.pos))
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
                entity.Set(components.NPCState.Script);
            }
            else
            {
                animation.Next = zzio.AnimationType.Walk0;
                // TODO: Check whether player slerping to walking NPCs is a thing
            }
        }

        private bool UpdateWalking(
            float elapsedTime,
            in DefaultEcs.Entity entity,
            components.NPCType npcType,
            Location location,
            ref components.NPCMovement move)
        {
            if (npcType != components.NPCType.Flying)
                World.Publish(new messages.CreaturePlaceToGround(entity));

            // TODO: Add NPC ActorHeadIK handling while walking

            var moveDist = elapsedTime * move.Speed;
            var moveDelta = Vector3.Normalize(location.InnerForward with { Y = 0f }) * moveDist;
            if (move.DistanceWalked + moveDist < move.DistanceToTarget)
            {
                if (move.DistanceToTarget - move.DistanceWalked > MinSlerpDistance)
                {
                    var dir = location.InnerForward;
                    var targetDir = move.TargetPos - location.LocalPosition;
                    dir = MathEx.HorizontalSlerp(targetDir, dir, SlerpCurvature, SlerpSpeed * elapsedTime);
                    location.LookIn(dir); // ^ inverse arguments

                    entity.Get<components.PuppetActorMovement>().TargetDirection = dir;
                }

                location.LocalPosition += moveDelta;
                move.DistanceWalked += moveDist;
                return false;
            }
            else
            {
                (move.LastWaypointId, move.CurWaypointId, move.NextWaypointId) = (move.CurWaypointId, move.NextWaypointId, -1);
                move.LastTargetPos = move.TargetPos;
                move.DistanceWalked = 0f;
                move.DistanceToTarget = 0f;
                return true;
            }
        }
    }
}
