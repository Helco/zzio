using System.Numerics;

namespace zzre.game.components
{
    public struct HumanPhysics
    {
        public const float DefaultSpeedModifier = 1f;

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
        public bool ShouldCollideWithModels;
        public bool DidCollideWithWorld;

        public HumanPhysics(float colliderSize)
        {
            ColliderSize = colliderSize;
            GravityModifier = 1f;
            SpeedModifier = DefaultSpeedModifier;
            ShouldCollideWithModels = true;

            Velocity = Vector3.Zero;
            State = AnimationState.Idle;
            HitFloor = HitCeiling = IsDrowning = IsWading = false;
            DidCollideWithWorld = false;
        }
    }
}
