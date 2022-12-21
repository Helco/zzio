using System;
using System.Numerics;
using DefaultEcs.System;

using DoorState = zzre.game.components.behaviour.CityDoor.DoorState;

namespace zzre.game.systems;

public partial class BehaviourCityDoor : AEntitySetSystem<float>
{
    private const float MaxPlayerDistanceSqr = 3f * 3f;
    private const float MoveHeightFactor = 0.9f;

    private Location playerLocation => playerLocationLazy.Value;
    private readonly Game game;
    private readonly Lazy<Location> playerLocationLazy;
    private readonly DefaultEcs.EntitySet locks;
    private readonly IDisposable addedSubscription;

    public BehaviourCityDoor(ITagContainer diContainer) : base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, useBuffer: false)
    {
        game = diContainer.GetTag<Game>();
        playerLocationLazy = new Lazy<Location>(() => game.PlayerEntity.Get<Location>());
        addedSubscription = World.SubscribeComponentAdded<components.behaviour.CityDoor>(HandleComponentAdded);

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

    private void HandleComponentAdded(in DefaultEcs.Entity entity, in components.behaviour.CityDoor door)
    {
        var location = entity.Get<Location>();
        var clumpBuffers = entity.Get<ClumpBuffers>();
        var endYPosition = MathF.Sign(door.Speed) * clumpBuffers.Bounds.Size.Y * MoveHeightFactor;
        entity.Set(door with
        {
            StartPosition = location.LocalPosition,
            EndYPosition = endYPosition
        });
        entity.Set<components.Collidable>();
    }

    [Update]
    private void Update(
        float elapsedTime,
        Location location,
        ref components.behaviour.CityDoor door)
    {
        bool keepOpen = location.DistanceSquared(playerLocation) < MaxPlayerDistanceSqr;

        // TODO: Add city door unlock behaviour

        switch (door.State)
        {
            case DoorState.Closed when keepOpen && !door.IsLocked:
                door.State = DoorState.StartToOpen;
                break;

            case DoorState.Opened when !keepOpen:
                door.State = DoorState.StartToClose;
                break;

            case DoorState.Opening:
                door.CurYPosition += elapsedTime * door.Speed;
                if (MathF.Abs(door.CurYPosition) > door.EndYPosition)
                {
                    door.CurYPosition = MathF.CopySign(door.EndYPosition, door.Speed);
                    door.State = DoorState.Opened;
                }
                break;

            case DoorState.Closing:
                door.CurYPosition -= elapsedTime * door.Speed;
                if (MathF.Sign(door.Speed) != MathF.Sign(door.CurYPosition))
                {
                    door.CurYPosition = 0f;
                    door.State = DoorState.Closed;
                }
                break;

            // TODO: Play city door open/close sound samples
            case DoorState.StartToOpen:
                door.State = DoorState.Opening;
                break;
            case DoorState.StartToClose:
                door.State = DoorState.Closing;
                break;
        }

        location.LocalPosition = door.StartPosition + Vector3.UnitY * door.CurYPosition;
    }
}
