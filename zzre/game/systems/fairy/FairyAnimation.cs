namespace zzre.game.systems;
using System;
using System.Numerics;
using DefaultEcs.System;

public partial class FairyAnimation : AEntitySetSystem<float>
{
    private const float MaxForwardDiscrepancy = 0.52f;
    private const float MinBackwardDiscrepancy = 2.92f;
    private const float BlendDuration = 0.2f;

    public FairyAnimation(ITagContainer diContainer) : base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, useBuffer: false)
    {
    }

    [Update]
    private static void Update(
        float elapsedTime,
        in components.Velocity velocity,
        in components.ActorParts actorParts,
        ref components.FairyAnimation animation)
    {
        WingSpeed(elapsedTime, velocity, actorParts, ref animation);
        var next = GetNextAnimation(velocity, animation);
        SwitchAnimation(next, actorParts, ref animation);
    }

    private static void WingSpeed(
        float elapsedTime,
        in components.Velocity velocity,
        in components.ActorParts actorParts,
        ref components.FairyAnimation animation)
    {
        if (!actorParts.Wings.HasValue)
            return;

        var newWingSpeed = animation.WingSpeed;
        if (velocity.Value.Y <= 0f)
        {
            if (newWingSpeed <= 0f || newWingSpeed > 1000f)
                newWingSpeed = 0f;
            newWingSpeed -= elapsedTime * 1.5f;
            if (newWingSpeed < 0f)
                newWingSpeed = 0f;
        }
        else
            newWingSpeed = 2f;
        animation.WingSpeed = newWingSpeed;

        actorParts.Wings.Value.Set(new components.AnimationSpeed(newWingSpeed + 1f));
    }

    private static zzio.AnimationType GetNextAnimation(
        in components.Velocity velocity,
        in components.FairyAnimation animation)
    {
        var horVelocity = MathEx.SafeNormalize(velocity.Value with { Y = 0f });
        var horTargetDir = MathEx.SafeNormalize(animation.TargetDirection with { Y = 0f });
        var discrepancy = Vector3.DistanceSquared(horVelocity, horTargetDir);
        var isMoreRight = Vector3.Cross(horTargetDir, horVelocity).Y <= 0f;
        float speedSqr = velocity.Value.LengthSquared();

        if (velocity.Value.Y == 0f)
        {
            if (speedSqr <= 0.1f)
                return zzio.AnimationType.Idle0;
            else if (discrepancy < MaxForwardDiscrepancy)
                return zzio.AnimationType.Run;
            else if (discrepancy > MinBackwardDiscrepancy)
                return zzio.AnimationType.Back;
            else if (isMoreRight)
                return zzio.AnimationType.Right;
            else
                return zzio.AnimationType.Left;
        }
        else
        {
            if (speedSqr <= 0.1f ||
                (MathF.Abs(velocity.Value.X) < 0.1f && MathF.Abs(velocity.Value.Z) < 0.1f))
                return zzio.AnimationType.SpecialIdle0;
            else if (discrepancy < MaxForwardDiscrepancy)
                return zzio.AnimationType.FlyForward;
            else if (discrepancy > MinBackwardDiscrepancy)
                return zzio.AnimationType.FlyBack;
            else if (isMoreRight)
                return zzio.AnimationType.Right;
            else
                return zzio.AnimationType.Left;
        }
    }

    private static void SwitchAnimation(
        zzio.AnimationType nextAnimation,
        in components.ActorParts actorParts,
        ref components.FairyAnimation fairy)
    {
        var skeleton = actorParts.Body.Get<Skeleton>();
        var animationPool = actorParts.Body.Get<components.AnimationPool>();

        // as long as we only know of this one special case, let's keep the exception for safeguard
        if (nextAnimation is zzio.AnimationType.SpecialIdle0 && !animationPool.Contains(nextAnimation))
            nextAnimation = zzio.AnimationType.Idle0;

        if (nextAnimation != fairy.Current)
        {
            fairy.Current = nextAnimation;
            skeleton.BlendToAnimation(animationPool[nextAnimation], BlendDuration);
        }
        else if (fairy.Current == zzio.AnimationType.Idle0 &&
            skeleton.Animation == null &&
            animationPool.Contains(zzio.AnimationType.Idle1))
            skeleton.BlendToAnimation(animationPool[zzio.AnimationType.Idle1], BlendDuration);
    }
}
