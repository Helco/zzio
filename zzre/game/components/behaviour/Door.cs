using System.Numerics;

namespace zzre.game.components.behaviour
{
    public struct Door
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

        public readonly bool IsRight;
        public readonly float Speed;
        public readonly Quaternion StartRotation;
        public readonly StdItemId? KeyItemId;
        public DoorState State;
        public float CurAngle;
        public bool IsLocked;

        public Door(bool isRight, float speed, StdItemId? keyItemId, Quaternion startRotation = default)
        {
            IsRight = isRight;
            Speed = speed;
            KeyItemId = keyItemId;
            StartRotation = startRotation;
            State = default;
            CurAngle = default;
            IsLocked = keyItemId != null;
        }
    }
}
