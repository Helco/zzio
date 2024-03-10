namespace zzre.game.systems;

partial class HumanPhysics
{
    [Configuration(Description = "Base speed walking backwards")]
    public float SpeedBackward = -20f;
    [Configuration(Description = "Base speed walking forwards")]
    public float SpeedForward = 20f;
    [Configuration(Description = "Base speed walking sideways")]
    public float SpeedSide = 20f;
    [Configuration(Description = "Base vertical speed on intentional jumps")]
    public float SpeedJump = 19.5f;
    [Configuration(Description = "Unused?")]
    public float SpeedLook = 2f;
    [Configuration(Description = "Speed modifier when Amy hits ground")]
    public float SpeedFallFactor = 0.76923078f;
    [Configuration(Description = "Horizontal speed of forced jumps")]
    public float SpeedForcedJump = 20f;
    [Configuration(Description = "Speed modifier for forced jumps")]
    public float SpeedFactorForcedJump = 0.7f * 0.5f;
    [Configuration(Description = "Speed factor for jumping when walking")]
    public float SpeedFactorSmallJump = 0.4f;
    [Configuration(Description = "Speed factor for jumping when running")]
    public float SpeedFactorBigJump = 2.5f;
    [Configuration(Description = "Factor for SpeedJump when whirl jumping")]
    public float SpeedFactorWhirlJump = 2.5f;
    [Configuration(Description = "Friction applied to speed in moving direction")]
    public float MoveFriction = 0.002f;
    [Configuration(Description = "Friction applied to speed in orthogonal direction")]
    public float SlipFriction = 0.002f;
    [Configuration(Description = "Acceleration towards ground\n(Zanzarah is flat)")]
    public float Gravity = 22f;
    [Configuration(Description = "Gravity factor during whirl jumps")]
    public float WhirlJumpGravityFactor = 1.5f;
    [Configuration(Description = "How fast gravity increases during jump")]
    public float GravityModifierSpeed = 6f;
    [Configuration(Description = "Max total physics step\n(irrelevant in zzre)")]
    public float MaxElapsedTime = 0.5f;
    [Configuration(Description = "Cosine of max angle for side movements")]
    public float MaxSideControlAngle = 10f;
    [Configuration(Description = "Max Y portion of collision vector to count as horizontal collision\n(Only without collision velocity preservation)")]
    public float MaxCollisionYDir = 0.6f;
    [Configuration(Description = "Max horizontal speed for forced jumps to be triggered")]
    public float MaxForcedJumpSpeed = 0.2f;
    [Configuration(Description = "Min Y portion of collision vector to count as floor coolision")]
    public float MinFloorYDir = 0.4f;
    [Configuration(Description = "Min speed for Amy to be running")]
    public float MinRunSpeed = 0.7f;
    [Configuration(Description = "Additional collision radius for plant blockers")]
    public float PlantBlockerAddRadius = 0.75f;
    [Configuration(Description = "Half height of creature collision cylinder")]
    public float CreatureHalfHeight = 2.5f;
    [Configuration(Description = "Radius of creature collision cylinder")]
    public float CreatureRadius = 1.3f;
    [Configuration(Description = "Whether floor collision is necessary for jumping", IsInteger = true, Min = 0, Max = 1)]
    public bool CanJumpWithoutFloor;
    [Configuration(Description = "Whether gravity and friction are applied", IsInteger = true, Min = 0, Max = 1)]
    public bool UseWorldForces = true;
    [Configuration(Description = "Whether velocity is preserved on collision", IsInteger = true, Min = 0, Max = 1)]
    public bool PreserveVelocityAtCollision = true;
}
