using System.Numerics;

namespace zzre.game.components
{
    public struct HumanPhysics
    {
        public const float ExteriorSpeedModifier = 1f;
        public const float InteriorSpeedModifier = 0.6f;

        public enum AnimationState
        {
            Idle,
            Walk,
            Run,
            Fall,
            Jump
        }

        public Vector3 Velocity;
        public readonly float ColliderSize;
        public float GravityModifier;
        public float SpeedModifier;
        public AnimationState State;
        public bool HitFloor;
        public bool HitCeiling;
        public bool IsDrowning;
        public bool IsWading;
        public float DisableModelCollisionTimer;
        public bool DidCollideWithWorld;

        public HumanPhysics(float colliderSize)
        {
            ColliderSize = colliderSize;
            GravityModifier = 1f;
            SpeedModifier = ExteriorSpeedModifier;
            DisableModelCollisionTimer = 0f;

            Velocity = Vector3.Zero;
            State = AnimationState.Idle;
            HitFloor = HitCeiling = IsDrowning = IsWading = false;
            DidCollideWithWorld = false;
        }
    }
}
