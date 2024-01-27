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
    private readonly IDisposable disableAttackTriggerSubscription;
    private Location playerLocation => playerLocationLazy.Value;
    private readonly Lazy<Location> playerLocationLazy;

    public TriggerActivation(ITagContainer diContainer) : base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, useBuffer: true)
    {
        var game = diContainer.GetTag<Game>();
        playerLocationLazy = new Lazy<Location>(() => game.PlayerEntity.Get<Location>());
        sceneChangingSubscription = World.Subscribe<messages.SceneChanging>(HandleSceneChanging);
        sceneLoadedSubscription = World.Subscribe<messages.SceneLoaded>(HandleSceneLoaded);
        disableTriggerSubscription = World.Subscribe<GSModDisableTrigger>(HandleDisableTrigger);
        disableAttackTriggerSubscription = World.Subscribe<GSModDisableAttackTrigger>(HandleDisableTrigger);
    }

    public override void Dispose()
    {
        base.Dispose();
        sceneChangingSubscription.Dispose();
        sceneLoadedSubscription.Dispose();
        disableTriggerSubscription.Dispose();
        disableAttackTriggerSubscription.Dispose();
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
        var disabledTriggers = msg.GetGameState<GSModDisableTrigger>();
        var disabledAttackTriggers = msg.GetGameState<GSModDisableAttackTrigger>();

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
            if (disabledTriggers.Any(t => t.TriggerId == trigger.idx) ||
                disabledAttackTriggers.Any(t => t.TriggerId == trigger.idx))
                entity.Set<components.Disabled>();
        }
    }

    private void HandleDisableTrigger(in GSModDisableTrigger message) => HandleDisableTrigger(message.TriggerId);
    private void HandleDisableTrigger(in GSModDisableAttackTrigger message) => HandleDisableTrigger(message.TriggerId);
    private void HandleDisableTrigger(uint triggerId)
    {
        var triggers = World.GetComponents<Trigger>();
        var triggerEntities = World.GetEntities()
            .With<Trigger>()
            .AsEnumerable();
        foreach (var entity in triggerEntities)
        {
            if (triggers[entity].idx == triggerId)
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
                var box = Box.FromMinMax(trigger.pos, trigger.end);
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
