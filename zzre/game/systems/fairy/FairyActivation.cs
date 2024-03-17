using System;
using System.Collections.Generic;
using System.Numerics;
using DefaultEcs.System;
using zzio;

namespace zzre.game.systems;

public sealed partial class FairyActivation : ISystem<float>
{
    [Configuration(Description = "Y Offset for non-ground start points")]
    private float NonGroundYOffset = 5f;

    private readonly DefaultEcs.World ecsWorld;
    private readonly IDisposable sceneLoadedSubscription;
    private readonly IDisposable entityDisabledSubscription;
    private readonly IDisposable entityEnabledSubscription;
    private readonly IDisposable switchFairySubscription;
    private readonly DefaultEcs.EntitySet fairySet;
    private List<zzio.scn.Trigger> startPoints = [];

    public bool IsEnabled { get; set; } = true;

    public FairyActivation(ITagContainer diContainer)
    {
        ecsWorld = diContainer.GetTag<DefaultEcs.World>();
        sceneLoadedSubscription = ecsWorld.Subscribe<messages.SceneLoaded>(HandleSceneLoaded);
        entityDisabledSubscription = ecsWorld.SubscribeEntityDisabled(HandleEntityDisabled);
        entityEnabledSubscription = ecsWorld.SubscribeEntityEnabled(HandleEntityEnabled);
        switchFairySubscription = ecsWorld.Subscribe<messages.SwitchFairy>(HandleSwitchFairy);

        fairySet = ecsWorld
            .GetEntities()
            .With<components.FairyAnimation>()
            .AsSet();
    }

    public void Dispose()
    {
        sceneLoadedSubscription.Dispose();
        entityDisabledSubscription.Dispose();
        entityEnabledSubscription.Dispose();
        switchFairySubscription.Dispose();
        fairySet.Dispose();
    }

    private void HandleSceneLoaded(in messages.SceneLoaded msg)
    {
        startPoints.Clear();
        foreach (var trigger in msg.Scene.triggers)
        {
            if (trigger.type == zzio.scn.TriggerType.MultiplayerStartpoint)
                startPoints.Add(trigger);
        }
    }

    private void HandleEntityDisabled(in DefaultEcs.Entity entity)
    {
        entity.Remove<components.FairyPhysics>();
        entity.Remove<components.PlayerControls>();

        var actorParts = entity.Get<components.ActorParts>();
        actorParts.Body.Set(components.Visibility.Invisible);
        actorParts.Wings?.Set(components.Visibility.Invisible);
    }

    private void HandleEntityEnabled(in DefaultEcs.Entity entity)
    {
        if (ecsWorld.Get<components.PlayerEntity>().Entity == entity.Get<components.Parent>().Entity)
        {
            entity.Set(components.FairyPhysics.Default);
            entity.Set<components.PlayerControls>();
        }

        var actorParts = entity.Get<components.ActorParts>();
        actorParts.Body.Set(components.Visibility.Visible);
        actorParts.Wings?.Set(components.Visibility.Visible);

        var location = entity.Get<Location>();
        var (startPos, startDir) = FindFarthestStartPoint();
        location.LocalPosition = startPos;
        location.LookIn(startDir);
    }

    private void HandleSwitchFairy(in messages.SwitchFairy fairy)
    {
        ref var participant = ref fairy.Participant.Get<components.DuelParticipant>();
        if (participant.ActiveSlot >= 0)
        {
            participant.ActiveFairy.Disable();
            participant.ActiveSlot = -1;
        }

        var nextSlotI = fairy.ToSlot;
        if (nextSlotI < 0)
        {
            int i = 0;
            for (; i < Inventory.FairySlotCount; i++)
            {
                var slotI = (Math.Max(0, participant.ActiveSlot) + i) % Inventory.FairySlotCount;
                if (participant.Fairies[slotI].IsAlive &&
                    participant.Fairies[slotI].Get<InventoryFairy>().currentMHP > 0)
                {
                    nextSlotI = slotI;
                    break;
                }
            }
            if (nextSlotI < 0)
                return;
        }
        participant.ActiveSlot = nextSlotI;
        var nextFairy = participant.ActiveFairy;
        nextFairy.Enable();
    }

    private (Vector3 pos, Vector3 dir) FindFarthestStartPoint()
    {
        if (startPoints.Count == 0)
            throw new InvalidOperationException("No start points were found in scene");

        int bestTriggerI = -1;
        float bestDistanceSqr = float.PositiveInfinity;
        for (int i = 0; i < startPoints.Count; i++)
        {
            var curDistanceSqr = GetMinimalDistanceToFairies(startPoints[i].pos);
            if (curDistanceSqr < bestDistanceSqr)
            {
                bestDistanceSqr = curDistanceSqr;
                bestTriggerI = i;
            }
        }

        return (startPoints[bestTriggerI].pos, startPoints[bestTriggerI].dir);
    }

    private float GetMinimalDistanceToFairies(Vector3 point)
    {
        float distance = 10000f; // originally 100, but we are ditching the sqrts
        foreach (var fairy in fairySet.GetEntities())
            distance = Math.Min(distance, fairy.Get<Location>().DistanceSquared(point));
        return distance;
    }

    public void Update(float state)
    {
    }
}
