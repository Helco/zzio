using System;
using System.Linq;
using System.Numerics;
using DefaultEcs.System;
using zzio;
using zzio.scn;

namespace zzre.game.systems;

[Without(typeof(components.Disabled))]
public partial class TriggerActivation : AEntitySetSystem<float>
{
    private readonly float MaxLookingDistSqr = 0.81f;

    private readonly IDisposable sceneChangingSubscription;
    private readonly IDisposable sceneLoadedSubscription;
    private readonly IDisposable disableTriggerSubscription;
    private Location playerLocation => playerLocationLazy.Value;
    private readonly Lazy<Location> playerLocationLazy;

    public TriggerActivation(ITagContainer diContainer) : base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, useBuffer: true)
    {
        var game = diContainer.GetTag<Game>();
        playerLocationLazy = new Lazy<Location>(() => game.PlayerEntity.Get<Location>());
        sceneChangingSubscription = World.Subscribe<messages.SceneChanging>(HandleSceneChanging);
        sceneLoadedSubscription = World.Subscribe<messages.SceneLoaded>(HandleSceneLoaded);
        disableTriggerSubscription = World.Subscribe<zzio.GSModDisableTrigger>(HandleDisableTrigger);
    }

    public override void Dispose()
    {
        base.Dispose();
        sceneChangingSubscription.Dispose();
        sceneLoadedSubscription.Dispose();
        disableTriggerSubscription.Dispose();
    }

    private void HandleSceneChanging(in messages.SceneChanging _) => World //  Set is filtered by collider
        .GetEntities()
        .With<Trigger>()
        .DisposeAll();

    private void HandleSceneLoaded(in messages.SceneLoaded msg)
    {
        var triggersWithEntities = World.GetEntities()
            .With<Trigger>()
            .AsEnumerable()
            .Select(e => e.Get<Trigger>())
            .ToHashSet();

        foreach (var trigger in msg.Scene.triggers.Except(triggersWithEntities))
        {
            var entity = World.CreateEntity();
            entity.Set(new Location()
            {
                Parent = World.Get<Location>(),
                LocalPosition = trigger.pos,
                LocalRotation = trigger.dir.ToZZRotation()
            });
            entity.Set(trigger);
        }
    }

    private void HandleDisableTrigger(in GSModDisableTrigger message)
    {
        var triggers = World.GetComponents<Trigger>();
        var triggerEntities = World.GetEntities()
            .With<Trigger>()
            .AsEnumerable();
        foreach (var entity in triggerEntities)
        {
            if (triggers[entity].idx == message.TriggerId)
            {
                entity.Set<components.Disabled>();
                break;
            }
        }
    }

    [WithPredicate]
    private bool IsActivatableTrigger(in Trigger trigger) => trigger.colliderType != TriggerColliderType.Point;

    [Update]
    private void Update(
        float elapsedTime,
        in DefaultEcs.Entity entity,
        Location location,
        Trigger trigger)
    {
        if (ShouldBeActive(location, trigger))
            entity.Set<components.ActiveTrigger>();
        else
            entity.Remove<components.ActiveTrigger>();
    }

    private bool ShouldBeActive(Location location, Trigger trigger)
    {
        var playerPos = playerLocation.LocalPosition;
        switch (trigger.colliderType)
        {
            case TriggerColliderType.Point: return false;

            case TriggerColliderType.Box:
                var box = new Box(Vector3.Zero, trigger.size);
                if (!box.Intersects(location, playerPos))
                    return false;
                break;

            case TriggerColliderType.Sphere:
                var sphere = new Sphere(location.LocalPosition, trigger.radius);
                if (!sphere.Intersects(playerPos))
                    return false;
                break;

            default: throw new NotImplementedException($"Unimplemented trigger collider type {trigger.colliderType}");
        }

        if (!trigger.requiresLooking)
            return true;

        return Vector3.DistanceSquared(location.InnerForward, playerLocation.InnerForward) < MaxLookingDistSqr;
    }
}
