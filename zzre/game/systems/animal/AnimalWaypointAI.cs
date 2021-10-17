using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using DefaultEcs.System;
using zzio;
using zzio.scn;
using State = zzre.game.components.AnimalWaypointAI.State;

namespace zzre.game.systems
{
    public partial class AnimalWaypointAI : AEntitySetSystem<float>
    {
        private const float FleeDistanceSqr = 4f;
        private const float BreakoutDistanceSqr = FleeDistanceSqr * 0.5f;
        private const float FleeSpeed = 4f;
        private const float NonFleeSpeed = FleeSpeed * 0.3f;
        private const float MinWaypointDistanceSqr = 2.5f;
        private const float MaxWaypointDistanceSqr = 49f;
        private const float MinPlayerAngle = 0.6f;
        private const float GroundDistance = 5f;

        private readonly Game game;
        private readonly Scene scene;
        private readonly WorldCollider worldCollider;
        private readonly IDisposable sceneLoadedSubscription;
        private readonly IDisposable addSubscription;
        private Trigger[] waypoints = Array.Empty<Trigger>();

        public AnimalWaypointAI(ITagContainer diContainer) : base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, null, 0)
        {
            game = diContainer.GetTag<Game>();
            scene = diContainer.GetTag<Scene>();
            worldCollider = diContainer.GetTag<WorldCollider>();
            sceneLoadedSubscription = World.Subscribe<messages.SceneLoaded>(HandleSceneLoaded);
            addSubscription = World.SubscribeComponentAdded<components.AnimalWaypointAI>(HandleAddedComponent);
        }

        public override void Dispose()
        {
            base.Dispose();
            sceneLoadedSubscription.Dispose();
            addSubscription.Dispose();
        }

        private void HandleSceneLoaded(in messages.SceneLoaded message)
        {
            waypoints = scene.triggers
                .Where(t => t.type == TriggerType.AnimalWaypoint)
                .ToArray();
        }

        private void HandleAddedComponent(in DefaultEcs.Entity entity, in components.AnimalWaypointAI ai)
        {
            if (ai.Config.Crawls)
                PutOnGround(entity, ai);
        }

        [Update]
        private void Update(float elapsedTime,
            in DefaultEcs.Entity entity,
            Location location,
            ref components.AnimalWaypointAI ai)
        {
            var random = GlobalRandom.Get;
            var playerLocation = game.PlayerEntity.Get<Location>();
            var playerDistanceSqr = Vector3.DistanceSquared(location.GlobalPosition, playerLocation.GlobalPosition);
            var moveDistance = elapsedTime * ai.CurrentSpeed;
            var nextAnimation = null as AnimationType?;
            var loopNextAnimation = true;

            switch (ai.CurrentState)
            {
                case State.Idle:
                    if (ai.Config.Flees && playerDistanceSqr < FleeDistanceSqr)
                    {
                        ai.CurrentState = State.SearchTarget;
                        ai.CurrentSpeed = FleeSpeed;
                        ai.WalkAnimation = AnimationType.Walk1;
                        // TODO: play sound 43 or 44
                        break;
                    }
                    if (ai.CurIdleTime <= ai.Config.MaxIdleTime)
                        break;
                    if (ai.Config.Flees)
                    {
                        ai.CurIdleTime = random.NextFloat();
                        var nextAction = random.NextFloat();
                        if (nextAction > 0.5f)
                        {
                            ai.CurrentState = State.SearchTarget;
                            ai.CurrentSpeed = NonFleeSpeed;
                            ai.WalkAnimation = AnimationType.Walk0;
                            break;
                        }
                        nextAnimation = nextAction > 0.3f
                            ? AnimationType.Idle1
                            : AnimationType.Idle0;
                        break;
                    }
                    ai.CurrentState = State.SearchTarget;
                    ai.CurIdleTime = random.NextFloat() * ai.Config.MaxIdleTime;
                    ai.WalkAnimation = AnimationType.Walk0;
                    break;

                case State.SearchTarget:
                    var nextWaypoint = FindNextWaypoint(location.GlobalPosition, ai.CurrentWaypoint);
                    if (nextWaypoint == null)
                    {
                        ai.CurrentState = State.Idle;
                        break;
                    }
                    ai.CurrentState = State.Moving;
                    ai.LastWaypoint = ai.CurrentWaypoint;
                    ai.CurrentWaypoint = nextWaypoint;
                    ai.DistanceToWP = Vector3.Distance(nextWaypoint.pos.ToNumerics(), location.GlobalPosition);
                    ai.MovedDistance = 0f;
                    nextAnimation = ai.WalkAnimation;
                    loopNextAnimation = !ai.Config.FullAnimationCycles;
                    location.LookAt(nextWaypoint.pos.ToNumerics());
                    break;

                case State.Moving when ai.Config.FullAnimationCycles:
                    var body = entity.Get<components.ActorParts>().Body;
                    var skeleton = body.Get<Skeleton>();
                    if (skeleton.CurrentAnimation == null)
                    {
                        if (ai.MovedDistance <= ai.DistanceToWP)
                        {
                            nextAnimation = ai.WalkAnimation;
                            loopNextAnimation = false;
                            break;
                        }
                        ai.CurrentState = State.Idle;
                        ai.MovedDistance = 0f;
                        nextAnimation = AnimationType.Idle0;
                        break;
                    }
                    ai.MovedDistance += moveDistance;
                    location.LocalPosition += location.InnerForward * moveDistance;
                    if (ai.Config.Crawls)
                        PutOnGround(entity, ai);
                    break;

                case State.Moving when !ai.Config.FullAnimationCycles:
                    if (ai.Config.Flees)
                    {
                        if (playerDistanceSqr < FleeDistanceSqr)
                        {
                            ai.WalkAnimation = AnimationType.Walk1;
                            ai.CurrentSpeed = FleeSpeed;
                        }
                        if (playerDistanceSqr < BreakoutDistanceSqr)
                        {
                            // TODO: Test whether this chicken breakout behavior actually occurs
                            var playerToAnimal = Vector3.Normalize(location.GlobalPosition - playerLocation.GlobalPosition);
                            location.LocalPosition += playerToAnimal * moveDistance;
                            break;
                        }   
                    }
                    ai.MovedDistance += moveDistance;
                    if (ai.MovedDistance > ai.DistanceToWP)
                    {
                        moveDistance = ai.MovedDistance - ai.DistanceToWP;
                        ai.MovedDistance = 0f;
                        ai.CurrentState = State.Idle;
                        nextAnimation = AnimationType.Idle0;
                    }
                    location.LocalPosition += location.InnerForward * moveDistance;
                    if (ai.Config.Crawls)
                        PutOnGround(entity, ai);
                    break;
            }
            ai.CurIdleTime += elapsedTime;

            if (nextAnimation != null && entity.Has<components.ActorParts>())
            {
                var body = entity.Get<components.ActorParts>().Body;
                var skeleton = body.Get<Skeleton>();
                var animationPool = body.Get<components.AnimationPool>();
                // TODO: Blend animal animations
                if (animationPool.Contains(nextAnimation.Value))
                    skeleton.JumpToAnimation(animationPool[nextAnimation.Value], loopNextAnimation);
            }
        }

        private Trigger? FindNextWaypoint(Vector3 animalPos, Trigger? currentWaypoint)
        {
            var random = GlobalRandom.Get;
            var playerPos = game.PlayerEntity.Get<Location>().GlobalPosition;
            var animalToPlayer = Vector3.Normalize(playerPos - animalPos);

            var potentialWaypoints = waypoints
                .Where(wp =>
                {
                    var waypointPos = wp.pos.ToNumerics();
                    var distanceToAnimal = Vector3.DistanceSquared(animalPos, waypointPos);
                    if (distanceToAnimal <= MinWaypointDistanceSqr || distanceToAnimal >= MaxWaypointDistanceSqr)
                        return false;

                    var animalToWaypoint = Vector3.Normalize(waypointPos - animalPos);
                    var angleAcos = Vector3.Distance(animalToWaypoint, animalToPlayer);
                    return angleAcos > MinPlayerAngle;
                }).ToArray();
            if (potentialWaypoints.Length == 0)
                return null;
            return random.NextOf(potentialWaypoints);
        }

        private void PutOnGround(DefaultEcs.Entity entity, in components.AnimalWaypointAI ai)
        {
            var location = entity.Get<Location>();
            var cast = worldCollider.Cast(new Line(
                location.GlobalPosition + Vector3.UnitY * GroundDistance,
                location.GlobalPosition - Vector3.UnitY * GroundDistance));
            if (cast == null)
                return;
            location.LocalPosition = cast.Value.Point;

            if (!ai.Config.OrientsToGround)
                return;
            // if setUp is used anywhere else we should generalize this garbage
            var newRight = Vector3.Normalize(Vector3.Cross(cast.Value.Normal, location.GlobalForward));
            var newForward = Vector3.Normalize(Vector3.Cross(newRight, cast.Value.Normal));
            location.LookIn(newForward);
        }
    }
}
