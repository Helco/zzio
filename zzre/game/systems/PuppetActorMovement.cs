using System;
using System.Linq;
using System.Numerics;
using DefaultEcs.System;
using zzio.scn;

namespace zzre.game.systems;

public partial class PuppetActorMovement : AEntitySetSystem<float>
{
    private const float Curvature = 100f;
    private const float SlerpSpeed = 2f;
    private const float GroundFromOffset = 1f;
    private const float GroundToOffset = -7f;

    private readonly IDisposable sceneLoadedSubscription;
    private readonly IDisposable addedSubscription;
    private readonly IDisposable placeToGroundSubscription;
    private readonly IDisposable placeToTriggerSubscription;
    private WorldCollider worldCollider = null!;
    private Scene scene = null!;

    public PuppetActorMovement(ITagContainer diContainer) : base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, useBuffer: true)
    {
        sceneLoadedSubscription = World.Subscribe<messages.SceneLoaded>(HandleSceneLoaded);
        addedSubscription = World.SubscribeEntityComponentAdded<components.PuppetActorMovement>(HandleComponentAdded);
        placeToGroundSubscription = World.Subscribe<messages.CreaturePlaceToGround>(HandlePlaceToGround);
        placeToTriggerSubscription = World.Subscribe<messages.CreaturePlaceToTrigger>(HandlePlaceToTrigger);
    }

    public override void Dispose()
    {
        base.Dispose();
        addedSubscription.Dispose();
        placeToGroundSubscription.Dispose();
        placeToTriggerSubscription.Dispose();
    }

    private void HandleSceneLoaded(in messages.SceneLoaded message)
    {
        scene = message.Scene;
        worldCollider = World.Get<WorldCollider>();
    }

    private void HandleComponentAdded(in DefaultEcs.Entity entity, in components.PuppetActorMovement movement)
    {
        var location = entity.Get<Location>();
        entity.Set(movement with { TargetDirection = location.InnerForward });

        if (!entity.TryGet<components.ActorParts>(out var actorParts))
            return;
        var bodyLocation = actorParts.Body.Get<Location>();
        bodyLocation.LocalPosition = location.LocalPosition;
        bodyLocation.LocalRotation = location.LocalRotation;
    }

    private void HandlePlaceToGround(in messages.CreaturePlaceToGround msg) =>
        PlaceToGround(msg.Entity, msg.Entity.Get<Location>());

    private void HandlePlaceToTrigger(in messages.CreaturePlaceToTrigger msg)
    {
        var location = msg.Entity.Get<Location>();
        var triggerIdx = msg.TriggerIdx;
        var trigger = msg.TriggerIdx < 0
            ? scene.triggers.OrderBy(t => Vector3.DistanceSquared(t.pos, location.LocalPosition)).FirstOrDefault()
            : scene.triggers.FirstOrDefault(t => t.idx == triggerIdx);
        if (trigger == null || trigger.type == TriggerType.NpcStartpoint || trigger.type == TriggerType.NpcAttackPosition)
            return;

        // TODO: Check whether puppet to ground placement is actually correct
        location.LocalPosition = trigger.pos;
        if (msg.MoveToGround)
            PlaceToGround(msg.Entity, location);
        if (msg.OrientByTrigger)
            location.LookIn(trigger.dir with { Y = 0.0001f });

        var actorMove = msg.Entity.TryGet<components.PuppetActorMovement>();
        if (actorMove.HasValue)
            actorMove.Value.TargetDirection = location.InnerForward;
    }

    private void PlaceToGround(in DefaultEcs.Entity entity, Location location)
    {
        var colliderSphere = entity.Get<Sphere>();
        var cast = worldCollider.Cast(new Line(
            location.LocalPosition + Vector3.UnitY * GroundFromOffset,
            location.LocalPosition + Vector3.UnitY * GroundToOffset));
        if (cast != null)
            location.LocalPosition = cast.Value.Point + Vector3.UnitY * colliderSphere.Radius / 2f;
    }

    [Update]
    private static void Update(float elapsedTime,
        in components.PuppetActorMovement movement,
        in components.ActorParts actorParts,
        in Location parentLocation)
    {
        var actorLocation = actorParts.Body.Get<Location>();
        actorLocation.LocalPosition = parentLocation.LocalPosition; // definitly no interpolation for position

        // again weirdness from original engine
        var targetDir = movement.TargetDirection;
        targetDir.Y = parentLocation.InnerForward.Y;
        actorLocation.HorizontalSlerpIn(targetDir, Curvature, elapsedTime * SlerpSpeed);
    }
}
