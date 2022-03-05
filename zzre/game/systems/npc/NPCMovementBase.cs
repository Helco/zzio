using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using DefaultEcs.System;
using zzio.scn;

namespace zzre.game.systems
{
    public abstract class NPCMovementBase : AEntitySetSystem<float>
    {
        private const float MinSlerpDistance = 0.5f;
        private const float SlerpCurvature = 100f;
        private const float SlerpSpeed = 2f;

        private readonly Lazy<Location> playerLocationLazy;
        private readonly IDisposable sceneLoadedSubscription;
        protected readonly Scene scene;
        protected IReadOnlyDictionary<int, Trigger> waypointById = new Dictionary<int, Trigger>();
        protected IReadOnlyDictionary<int, Trigger> waypointByIdx = new Dictionary<int, Trigger>();
        protected ILookup<int, Trigger> waypointsByCategory = Enumerable.Empty<Trigger>().ToLookup(t => 0);

        protected Location PlayerLocation => playerLocationLazy.Value;

        protected NPCMovementBase(ITagContainer diContainer, Func<object, DefaultEcs.World, DefaultEcs.EntitySet> factory, bool useBuffer)
            : base(diContainer.GetTag<DefaultEcs.World>(), factory, useBuffer: true)
        {
            var game = diContainer.GetTag<Game>();
            playerLocationLazy = new Lazy<Location>(() => game.PlayerEntity.Get<Location>());
            scene = diContainer.GetTag<Scene>();
            sceneLoadedSubscription = World.Subscribe<messages.SceneLoaded>(HandleSceneLoaded);
        }

        public override void Dispose()
        {
            base.Dispose();
            sceneLoadedSubscription.Dispose();
        }

        private void HandleSceneLoaded(in messages.SceneLoaded _)
        {
            var waypoints = scene.triggers.Where(t => t.type == TriggerType.Waypoint).ToArray();
            waypointById = waypoints
                .GroupBy(wp => (int)wp.ii1)
                .ToDictionary(group => group.Key, group => group.First());
            waypointByIdx = waypoints.ToDictionary(wp => (int)wp.idx, wp => wp);
            waypointsByCategory = waypoints.ToLookup(wp => (int)wp.ii2);
        }

        protected bool UpdateWalking(
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