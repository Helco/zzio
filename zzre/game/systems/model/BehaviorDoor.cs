using System;
using System.Numerics;
using DefaultEcs.System;

using DoorState = zzre.game.components.behaviour.Door.DoorState;

namespace zzre.game.systems
{
    public partial class BehaviourDoor : AEntitySetSystem<float>
    {
        private const float MaxPlayerDistanceSqr = 3f * 3f;
        private const float MaxPlayerYDistance = 1.5f;
        private const float MaxAngle = 90f;

        private Location playerLocation => playerLocationLazy.Value;
        private readonly Game game;
        private readonly Lazy<Location> playerLocationLazy;
        private readonly DefaultEcs.EntitySet locks;
        private readonly IDisposable addedSubscription;

        public BehaviourDoor(ITagContainer diContainer) : base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, useBuffer: false)
        {
            game = diContainer.GetTag<Game>();
            playerLocationLazy = new Lazy<Location>(() => game.PlayerEntity.Get<Location>());
            addedSubscription = World.SubscribeComponentAdded<components.behaviour.Door>(HandleComponentAdded);

            locks = World
                .GetEntities()
                .With<components.behaviour.Lock>()
                .AsSet();
        }

        public override void Dispose()
        {
            base.Dispose();
            addedSubscription?.Dispose();
            locks.Dispose();
        }

        private void HandleComponentAdded(in DefaultEcs.Entity entity, in components.behaviour.Door door)
        {
            var location = entity.Get<Location>();
            entity.Set(new components.behaviour.Door(door.IsRight, door.Speed, door.KeyItemId, location.LocalRotation));
            entity.Set<components.Collidable>();
        }

        [Update]
        private void Update(
            float elapsedTime,
            Location location,
            ref components.behaviour.Door door)
        {
            bool keepOpen =
                location.DistanceSquared(playerLocation) < MaxPlayerDistanceSqr &&
                MathF.Abs(location.LocalPosition.Y - playerLocation.LocalPosition.Y) < MaxPlayerYDistance;

            // TODO: Add door unlock behaviour

            switch(door.State)
            {
                case DoorState.Closed when keepOpen && !door.IsLocked:
                    door.State = DoorState.StartToOpen;
                    break;

                case DoorState.Opened when !keepOpen:
                    door.State = DoorState.StartToClose;
                    break;

                case DoorState.Opening:
                    door.CurAngle += elapsedTime * door.Speed;
                    if (MathF.Abs(door.CurAngle) > MaxAngle)
                    {
                        door.CurAngle = MathF.CopySign(MaxAngle, door.Speed);
                        door.State = DoorState.Opened;
                    }
                    break;

                case DoorState.Closing:
                    door.CurAngle -= elapsedTime * door.Speed;
                    if (MathF.Sign(door.Speed) != MathF.Sign(door.CurAngle))
                    {
                        door.CurAngle = 0f;
                        door.State = DoorState.Closed;
                    }
                    break;

                // TODO: Play door open/close sound samples
                case DoorState.StartToOpen:
                    door.State = DoorState.Opening;
                    break;
                case DoorState.StartToClose:
                    door.State = DoorState.Closing;
                    break;
            }

            location.LocalRotation = door.StartRotation *
                Quaternion.CreateFromAxisAngle(Vector3.UnitY, door.CurAngle * MathEx.DegToRad);
        }
    }
}
