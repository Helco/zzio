using System;
using System.Numerics;
using DefaultEcs.System;
using zzio;
using zzio.scn;

namespace zzre.game.systems
{
    [Without(typeof(components.Disabled))]
    public partial class TriggerActivation : AEntitySetSystem<float>
    {
        private readonly float MaxLookingDistSqr = 0.81f;

        private readonly IDisposable sceneLoadedSubscription;
        private readonly IDisposable disableTriggerSubscription;
        private readonly Scene scene;
        private Location playerLocation => playerLocationLazy.Value;
        private readonly Lazy<Location> playerLocationLazy;

        public TriggerActivation(ITagContainer diContainer) : base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, useBuffer: true)
        {
            var game = diContainer.GetTag<Game>();
            scene = diContainer.GetTag<Scene>();
            playerLocationLazy = new Lazy<Location>(() => game.PlayerEntity.Get<Location>());
            sceneLoadedSubscription = World.Subscribe<messages.SceneLoaded>(HandleSceneLoaded);
            disableTriggerSubscription = World.Subscribe<zzio.GSModDisableTrigger>(HandleDisableTrigger);
        }

        public override void Dispose()
        {
            base.Dispose();
            sceneLoadedSubscription?.Dispose();
            disableTriggerSubscription?.Dispose();
        }

        private void HandleSceneLoaded(in messages.SceneLoaded msg)
        {
            // TODO: TriggerActivation creates double triggers for e.g. NPCs

            foreach (var trigger in scene.triggers)
            {
                var entity = World.CreateEntity();
                var location = new Location();
                location.Parent = World.Get<Location>();
                location.LocalPosition = trigger.pos;
                location.LocalRotation = trigger.dir.ToZZRotation();
                entity.Set(location);
                entity.Set(trigger);
            }
        }

        private void HandleDisableTrigger(in GSModDisableTrigger message)
        {
            var triggers = World.GetComponents<Trigger>();
            foreach (ref readonly var entity in Set.GetEntities())
            {
                if (triggers[entity].idx == message.TriggerId)
                {
                    entity.Set<components.Disabled>();
                    break;
                }
            }
        }

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
}
