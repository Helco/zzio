using System;
using DefaultEcs.System;

namespace zzre.game.systems
{
    [PauseDuringUIScreen]
    public partial class NPCActivator : AEntitySetSystem<float>
    {
        private const float NPCComfortZoneSqr = 2.5f * 2.5f;

        private Location playerLocation => playerLocationLazy.Value;
        private readonly Lazy<Location> playerLocationLazy;

        public NPCActivator(ITagContainer diContainer) : base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, useBuffer: true)
        {
            World.SetMaxCapacity<components.ActiveNPC>(1);
            var game = diContainer.GetTag<Game>();
            playerLocationLazy = new Lazy<Location>(() => game.PlayerEntity.Get<Location>());

            Set.EntityRemoved += HandleEntityRemoved;
        }

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
            Location npcLocation,
            ref components.NonFairyAnimation animation)
        {
            var currentDistanceSqr = playerLocation.DistanceSquared(npcLocation);
            var isInComfortZone = currentDistanceSqr < NPCComfortZoneSqr;
            animation.CanUseAlternativeIdles = !isInComfortZone;

            DefaultEcs.Entity? otherNPC = null;
            float otherDistanceSqr = float.PositiveInfinity;
            if (World.Has<components.ActiveNPC>())
            {
                otherNPC = World.Get<components.ActiveNPC>().Entity;
                otherDistanceSqr = playerLocation.DistanceSquared(otherNPC.Value.Get<Location>());
            }

            if (!isInComfortZone && entity == otherNPC)
                World.Remove<components.ActiveNPC>();
            else if (isInComfortZone && currentDistanceSqr < otherDistanceSqr)
                World.Set(new components.ActiveNPC(entity));
        }
    }
}
