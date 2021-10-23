using System;

namespace zzre.game.components
{
    public readonly struct PhysicParameters
    {
        // some of these were hardcoded into the engine, some were modifiable but never were actually modified

        public float SpeedBackward { get; init; }
        public float SpeedForward { get; init; }
        public float SpeedSide { get; init; }
        public float SpeedJump { get; init; }
        public float SpeedLook { get; init; }
        public float SpeedFallFactor { get; init; }
        public float SpeedForcedJump { get; init; }
        public float SpeedFactorForcedJump { get; init; }
        public float SpeedFactorSmallJump { get; init; }
        public float SpeedFactorBigJump { get; init; }
        public float SpeedFactorWhirlJump { get; init; }
        public float MoveFriction { get; init; }
        public float SlipFriction { get; init; }
        public float Gravity { get; init; }
        public float WhirlJumpGravityFactor { get; init; }
        public float GravityModifierSpeed { get; init; }
        public float MaxElapsedTime { get; init; }
        public float MaxSideControlAngle { get; init; }
        public float MaxCollisionYDir { get; init; } 
        public float MaxForcedJumpSpeed { get; init; }
        public float MinFloorYDir { get; init; }
        public float MinRunSpeed { get; init; }
        public bool CanJumpWithoutFloor { get; init; }
        public bool UseWorldForces { get; init; }
        public bool PreserveVelocityAtCollision { get; init; }

        public static readonly PhysicParameters Standard = new PhysicParameters()
        {
            SpeedBackward = -20f,
            SpeedForward = 20f,
            SpeedSide = 20f,
            SpeedJump = 19.5f,
            SpeedLook = 2f,
            SpeedFallFactor = 0.76923078f,
            SpeedForcedJump = 20f,
            SpeedFactorForcedJump = 0.7f * 0.5f,
            SpeedFactorSmallJump = 0.4f,
            SpeedFactorBigJump = 2.5f,
            SpeedFactorWhirlJump = 2.5f,
            MoveFriction = 0.002f,
            SlipFriction = 0.002f,
            Gravity = 22f,
            WhirlJumpGravityFactor = 1.5f,
            GravityModifierSpeed = 6f,
            MaxElapsedTime = 0.5f,
            MaxSideControlAngle = 10f,
            MaxCollisionYDir = 0.6f,
            MaxForcedJumpSpeed = 0.2f,
            MinFloorYDir = 0.4f,
            MinRunSpeed = 0.7f,
            CanJumpWithoutFloor = false,
            UseWorldForces = true,
            PreserveVelocityAtCollision = true
        };
    }
}
