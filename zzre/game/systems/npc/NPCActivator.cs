using System;
using DefaultEcs.System;

namespace zzre.game.systems;

[PauseDuring(PauseTrigger.UIScreen)]
public partial class NPCActivator : AEntitySetSystem<float>
{
    private const float NPCComfortZoneSqr = 2.5f * 2.5f;

    private Location PlayerLocation => World.Get<components.PlayerEntity>().Entity.Get<Location>();
    private readonly IDisposable playerEnteredSubscription;

    public NPCActivator(ITagContainer diContainer) : base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, useBuffer: true)
    {
        World.SetMaxCapacity<components.ActiveNPC>(1);
        playerEnteredSubscription = World.Subscribe<messages.PlayerEntered>(HandlePlayerEntered);

        Set.EntityRemoved += HandleEntityRemoved;
    }

    public override void Dispose()
    {
        base.Dispose();
        playerEnteredSubscription.Dispose();
    }

    private void HandlePlayerEntered(in messages.PlayerEntered _) =>
        World.Remove<components.ActiveNPC>();

    private void HandleEntityRemoved(in DefaultEcs.Entity entity)
    {
        if (World.Has<components.ActiveNPC>() &&
            World.Get<components.ActiveNPC>().Entity == entity)
            World.Remove<components.ActiveNPC>();
    }

    [WithPredicate]
    private static bool IsActivatableNPC(in components.NPCType type) =>
        type == components.NPCType.Biped ||
        type == components.NPCType.Flying ||
        type == components.NPCType.Item;

    [Update]
    private void Update(
        in DefaultEcs.Entity entity,
        Location npcLocation)
    {
        var currentDistanceSqr = PlayerLocation.DistanceSquared(npcLocation);
        var isInComfortZone = currentDistanceSqr < NPCComfortZoneSqr;

        var nonFairyAnim = entity.TryGet<components.NonFairyAnimation>();
        if (nonFairyAnim.HasValue)
            nonFairyAnim.Value.CanUseAlternativeIdles = !isInComfortZone;

        DefaultEcs.Entity? otherNPC = null;
        float otherDistanceSqr = float.PositiveInfinity;
        if (World.Has<components.ActiveNPC>())
        {
            otherNPC = World.Get<components.ActiveNPC>().Entity;
            otherDistanceSqr = PlayerLocation.DistanceSquared(otherNPC.Value.Get<Location>());
        }

        if (!isInComfortZone && entity == otherNPC)
            World.Remove<components.ActiveNPC>();
        else if (isInComfortZone && currentDistanceSqr < otherDistanceSqr)
            World.Set(new components.ActiveNPC(entity));
    }
}
