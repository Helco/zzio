using System;
using System.Numerics;
using DefaultEcs.System;

using DoorState = zzre.game.components.behaviour.Door.DoorState;

namespace zzre.game.systems;

public partial class BehaviourDoor : AEntitySetSystem<float>
{
    //private const float MaxLockDistanceSqr = 8f;
    private const float MaxPlayerDistanceSqr = 3f * 3f;
    private const float MaxPlayerYDistance = 1.5f;
    private const float MaxAngle = 90f;

    private Location PlayerLocation => playerLocationLazy.Value;
    private readonly Game game;
    private readonly Lazy<Location> playerLocationLazy;
    private readonly DefaultEcs.EntitySet locks;
    private readonly IDisposable addedSubscription;

    public BehaviourDoor(ITagContainer diContainer) : base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, useBuffer: false)
    {
        game = diContainer.GetTag<Game>();
        playerLocationLazy = new Lazy<Location>(() => game.PlayerEntity.Get<Location>());
        addedSubscription = World.SubscribeEntityComponentAdded<components.behaviour.Door>(HandleComponentAdded);

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
        in DefaultEcs.Entity doorEntity,
        float elapsedTime,
        Location location,
        ref components.behaviour.Door door)
    {
        bool playerIsNear =
            location.DistanceSquared(PlayerLocation) < MaxPlayerDistanceSqr &&
            MathF.Abs(location.LocalPosition.Y - PlayerLocation.LocalPosition.Y) < MaxPlayerYDistance;

        if (door.IsLocked && door.KeyItemId is not null && playerIsNear &&
            game.PlayerEntity.Get<Inventory>().Contains(door.KeyItemId.Value) &&
            game.PlayerEntity.Get<components.GameFlow>() == components.GameFlow.Normal)
        {
            var myLock = FindLockNearTo(location);
            if (myLock is null)
                door.IsLocked = false;
            else
            {
                World.Publish(new messages.UnlockDoor(doorEntity, myLock.Value, door.KeyItemId.Value));
                return;
            }
        }

        switch (door.State)
        {
            case DoorState.Closed when playerIsNear && !door.IsLocked:
                door.State = DoorState.StartToOpen;
                break;

            case DoorState.Opened when !playerIsNear:
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

    private DefaultEcs.Entity? FindLockNearTo(Location doorLocation)
    {
        var minLockEntity = default(DefaultEcs.Entity?);
        var minLockDistSqr = float.MaxValue;
        foreach (var lockEntity in locks.GetEntities())
        {
            var lockDist = lockEntity.Get<Location>().DistanceSquared(doorLocation);
            if (lockDist < minLockDistSqr)
            {
                minLockDistSqr = lockDist;
                minLockEntity = lockEntity;
            }
        }
        return minLockEntity;
    }
}
