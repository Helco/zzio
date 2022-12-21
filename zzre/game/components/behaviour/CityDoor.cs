using System.Numerics;

namespace zzre.game.components.behaviour;

public struct CityDoor
{
    public enum DoorState
    {
        Closed,
        Opened,
        Opening,
        Closing,
        StartToOpen,
        StartToClose
    }

    public readonly float Speed;
    public readonly StdItemId? KeyItemId;
    public readonly Vector3 StartPosition { get; init; }
    public readonly float EndYPosition { get; init; }
    public DoorState State;
    public float CurYPosition;
    public bool IsLocked;

    public CityDoor(float speed, StdItemId? keyItemId)
    {
        Speed = speed;
        KeyItemId = keyItemId;
        StartPosition = default;
        EndYPosition = default;
        State = default;
        CurYPosition = default;
        IsLocked = keyItemId != null;
    }
}
