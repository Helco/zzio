using System;
using System.Numerics;
using DefaultEcs.System;
using zzio.scn;

namespace zzre.game.systems
{
    public partial class TriggerActivation : AEntitySetSystem<float>
    {
        private readonly float MaxLookingDistSqr = 0.81f;

        private readonly IDisposable sceneLoadedSubscription;
        private readonly Scene scene;
        private Location playerLocation => playerLocationLazy.Value;
        private readonly Lazy<Location> playerLocationLazy;

        public TriggerActivation(ITagContainer diContainer) : base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, useBuffer: true)
        {
            var game = diContainer.GetTag<Game>();
            scene = diContainer.GetTag<Scene>();
            playerLocationLazy = new Lazy<Location>(() => game.PlayerEntity.Get<Location>());
            sceneLoadedSubscription = World.Subscribe<messages.SceneLoaded>(HandleSceneLoaded);
        }

        public override void Dispose()
        {
            base.Dispose();
            sceneLoadedSubscription?.Dispose();
        }

        private void HandleSceneLoaded(in messages.SceneLoaded msg)
        {
            foreach (var trigger in scene.triggers)
            {
                var entity = World.CreateEntity();
                var location = new Location();
                location.Parent = World.Get<Location>();
                location.LocalPosition = trigger.pos.ToNumerics();
                location.LocalRotation = trigger.dir.ToNumericsRotation();
                entity.Set(location);
                entity.Set(trigger);
            }
        }

        [Update]
        private void Update(
            float elapsedTime,
            in DefaultEcs.Entity entity,
            Location location,
            Trigger trigger)
        {
#pragma warning disable DEA0005 // Entity modification method in Update (false positive)
            if (ShouldBeActive(location, trigger))
                entity.Set<components.ActiveTrigger>();
            else
                entity.Remove<components.ActiveTrigger>();
#pragma warning restore DEA0005
        }

        private bool ShouldBeActive(Location location, Trigger trigger)
        {
            var playerPos = playerLocation.LocalPosition;
            switch (trigger.colliderType)
            {
                case TriggerColliderType.Point: return false;

                case TriggerColliderType.Box:
                    var box = new Box(Vector3.Zero, trigger.size.ToNumerics());
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
