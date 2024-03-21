namespace zzre.game.systems;
using System;
using System.Linq;
using System.Numerics;
using DefaultEcs.System;
using zzio;
using zzio.scn;
using State = zzre.game.components.AnimalWaypointAI.State;

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

    private Location PlayerLocation =>
        World.Get<components.PlayerEntity>().Entity.Get<Location>();

    private readonly Random Random = Random.Shared;
    private readonly IDisposable sceneLoadedSubscription;
    private readonly IDisposable addSubscription;
    private Trigger[] waypoints = [];

    public AnimalWaypointAI(ITagContainer diContainer) : base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, useBuffer: true)
    {
        sceneLoadedSubscription = World.Subscribe<messages.SceneLoaded>(HandleSceneLoaded);
        addSubscription = World.SubscribeEntityComponentAdded<components.AnimalWaypointAI>(HandleAddedComponent);
    }

    public override void Dispose()
    {
        base.Dispose();
        sceneLoadedSubscription.Dispose();
        addSubscription.Dispose();
    }

    private void HandleSceneLoaded(in messages.SceneLoaded message)
    {
        var scene = message.Scene;
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
        var playerLocation = PlayerLocation;
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
                    World.Publish(new messages.SpawnSample(
                        $"resources/audio/sfx/specials/_s0{Random.Next(43, 45)}.wav",
                        Position: location.GlobalPosition));
                    break;
                }
                if (ai.CurIdleTime <= ai.Config.MaxIdleTime)
                    break;
                if (ai.Config.Flees)
                {
                    ai.CurIdleTime = Random.NextFloat();
                    var nextAction = Random.NextFloat();
                    if (nextAction > 0.5f)
                    {
                        ai.CurrentState = State.SearchTarget;
                        ai.CurrentSpeed = NonFleeSpeed;
                        ai.WalkAnimation = AnimationType.Walk0;
                    }
                    else
                        nextAnimation = nextAction > 0.3f
                            ? AnimationType.Idle1
                            : AnimationType.Idle0;
                }
                else
                {
                    ai.CurrentState = State.SearchTarget;
                    ai.CurIdleTime = Random.NextFloat() * ai.Config.MaxIdleTime;
                    ai.WalkAnimation = AnimationType.Walk0;
                }
                break;

            case State.SearchTarget:
                var nextWaypoint = FindNextWaypoint(location.GlobalPosition, ai.CurrentWaypoint, playerLocation.GlobalPosition);
                if (nextWaypoint == null)
                {
                    ai.CurrentState = State.Idle;
                    break;
                }
                ai.CurrentState = State.Moving;
                ai.LastWaypoint = ai.CurrentWaypoint;
                ai.CurrentWaypoint = nextWaypoint;
                ai.DistanceToWP = Vector3.Distance(nextWaypoint.pos, location.GlobalPosition);
                ai.MovedDistance = 0f;
                nextAnimation = ai.WalkAnimation;
                loopNextAnimation = !ai.Config.FullAnimationCycles;
                location.LookAt(nextWaypoint.pos);
                break;

            case State.Moving when ai.Config.FullAnimationCycles:
                var skeleton = entity.TryGet<components.ActorParts>().GetValueOrDefault()
                    .Body.TryGet<Skeleton>().GetValueOrDefault();
                if (skeleton?.Animation == null)
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
                        var playerToAnimal = Vector3.Normalize(location.GlobalPosition - playerLocation.GlobalPosition);
                        location.LocalPosition += playerToAnimal * moveDistance;
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

        var body = entity.TryGet<components.ActorParts>().GetValueOrDefault().Body;
        if (nextAnimation != null && body != default)
        {
            var skeleton = body.Get<Skeleton>();
            var animationPool = body.Get<components.AnimationPool>();
            // TODO: Blend animal animations
            if (animationPool.Contains(nextAnimation.Value))
                skeleton.JumpToAnimation(animationPool[nextAnimation.Value], loopNextAnimation);
        }
    }

    private Trigger? FindNextWaypoint(Vector3 animalPos, Trigger? currentWaypoint, Vector3 playerPos)
    {
        var animalToPlayer = Vector3.Normalize(playerPos - animalPos);

        var potentialWaypoints = waypoints
            .Where(wp =>
            {
                var waypointPos = wp.pos;
                var distanceToAnimal = Vector3.DistanceSquared(animalPos, waypointPos);
                if (distanceToAnimal <= MinWaypointDistanceSqr || distanceToAnimal >= MaxWaypointDistanceSqr)
                    return false;

                var animalToWaypoint = Vector3.Normalize(waypointPos - animalPos);
                var angleAcos = Vector3.Distance(animalToWaypoint, animalToPlayer);
                return angleAcos > MinPlayerAngle;
            }).ToArray();
        if (potentialWaypoints.Length == 0)
            return null;
        return Random.NextOf(potentialWaypoints);
    }

    private void PutOnGround(DefaultEcs.Entity entity, in components.AnimalWaypointAI ai)
    {
        var location = entity.Get<Location>();
        var worldCollider = World.Get<WorldCollider>();
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
