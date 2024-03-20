using System;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using DefaultEcs.System;

namespace zzre.game.systems;

public sealed partial class FairyPhysics : AEntitySetSystem<float>
{
    [Configuration(Description = "If frametime is greater than this, no physics update happens")]
    private float MaxElapsedTime = 0.5f;
    [Configuration(Description = "Max number of micro steps", IsInteger = true, Min = 1)]
    private int MaxMicroSteps = 400;
    [Configuration(Description = "Min Y portion of collision vector to count as floor coolision")]
    private float MinFloorYDir = 0.8f;
    [Configuration(Description = "Min bounce factor on world collisions")]
    private float MinCollisionBounce = 0.00001f;
    [Configuration(Description = "Bounce factor on world collision relative to scaled angle")]
    private float CollisionBounceFactor = 1.01f;
    [Configuration(Description = "The acceleration of the apple towards the head of Sir Isaac Newton",
        Key = "/zanzarah.net.TEST_GRAVITY")]
    private float Gravity = 9.81f;
    [Configuration(Description = "Speed for going/flying forwards")]
    private float SpeedForward = 150f;
    [Configuration(Description = "Speed for going/flying backwards\n(should be negative)")]
    private float SpeedBackward = -150f;
    [Configuration(Description = "Speed for going/flying sideways")]
    private float SpeedSideways = 150f;
    [Configuration(Description = "Ceiling distance at which the greates push-down acceleration is applied")]
    private float MinCeilingDistance = 0.01f;
    [Configuration(Description = "Ceiling distance at which the lowest push-down acceleration is applied")]
    private float MaxCeilingDistance = 2f;
    [Configuration(Description = "Push-down acceleration of the ceiling at a distance of 1")]
    private float CeilingForce = -3f;
    [Configuration(Description = "Factor by which the creature collider radius is grown")]
    private float CreatureRadiusFactor = 3f;
    [Configuration(Description = "Minimum distance at which creature push-away acceleration is applied and is at its highest")]
    private float MinCreatureDistance = 0.01f;
    [Configuration(Description = "A general friction based on effective velocity",
        Key = "/zanzarah.net.TEST_ML_FRICTION")]
    private float MLFriction = 0.5f;
    [Configuration(Description = "A floor sliding friction based on angle to collision",
        Key = "/zanzarah.net.TEST_M_FRICTION")]
    private float MFriction = 1f;
    [Configuration(Description = "An additional factor for the floor sliding friction")]
    private float MFrictionFactor = 1.5f;
    [Configuration(Description = "A deadzone for downward velocity when standing on floor")]
    private float FloorDejitterBand = 0.15f;
    [Configuration(Description = "Max horizontal speed relative to moveSpeed attribute")]
    private float MaxSpeedFactor = 3f;
    [Configuration(Description = "Max upwards speed to trigger jumps")]
    private float MaxJumpVelocity = 5f;
    [Configuration(Description = "Upwards speed upon triggering a jump")]
    private float JumpVelocity = 2.5f;

    private readonly IDisposable configDisposable;
    private readonly IDisposable sceneLoadedSubscription;
    private readonly DefaultEcs.EntitySet fairySet;
    private WorldCollider worldCollider = null!;
    private zzio.scn.Trigger? ceiling;
    private bool isInterior;

    public FairyPhysics(ITagContainer diContainer) : base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, useBuffer: false)
    {
        World.SetMaxCapacity<components.FairyPhysics>(1);
        configDisposable = diContainer.GetConfigFor(this);
        sceneLoadedSubscription = World.Subscribe<messages.SceneLoaded>(HandleSceneLoaded);

        fairySet = World
            .GetEntities()
            .With<components.FairyAnimation>()
            .AsSet();
    }

    public override void Dispose()
    {
        base.Dispose();
        configDisposable?.Dispose();
        sceneLoadedSubscription?.Dispose();
        fairySet?.Dispose();
    }

    private void HandleSceneLoaded(in messages.SceneLoaded message)
    {
        worldCollider = World.Get<WorldCollider>();
        ceiling = message.Scene.triggers.FirstOrDefault(t => t.type == zzio.scn.TriggerType.Ceiling);
        isInterior = message.Scene.dataset.isInterior;
    }

    [Update]
    private void Update(float elapsedTime,
        DefaultEcs.Entity entity,
        Location location,
        ref components.FairyPhysics state,
        ref components.PlayerControls controls,
        ref components.Velocity velocityComp,
        zzio.InventoryFairy invFairy,
        in Sphere colliderSphere)
    {
        if (elapsedTime > MaxElapsedTime)
        {
            state.IsRunning = false;
            return;
        }

        var startVelocity = velocityComp.Value;
        var startSpeed = startVelocity.Length();
        var maxMoveDistance = startSpeed * elapsedTime;
        var maxMoveDistanceSqr = maxMoveDistance * maxMoveDistance;
        var startPos = location.LocalPosition;
        state.HitFloor = state.HitCeiling = state.IsRunning = false;
        Vector3 dirCollisionToMe = Vector3.Zero;
        Vector3 acceleration = Vector3.Zero;

        int stepCount = (int)(1 + startSpeed / colliderSphere.RadiusSq);
        stepCount = Math.Min(stepCount, MaxMicroSteps);
        var elapsedStepTime = elapsedTime / stepCount;
        var nextPos = startPos;
        var effectiveVelocity = startVelocity; // the velocity after micro steps
        for (int i = 0; i < stepCount; i++)
        {
            if (Vector3.DistanceSquared(nextPos, startPos) > maxMoveDistanceSqr)
                break;
            MicroStep(ref state, ref nextPos, ref effectiveVelocity, startPos, elapsedStepTime, colliderSphere.Radius, ref dirCollisionToMe);
            Validate();
        }
        var effectiveSpeed = effectiveVelocity.Length();
        location.LocalPosition = nextPos;

        acceleration += Vector3.UnitY * -Gravity;
        ApplyControls(ref acceleration, ref state, controls, location, invFairy.moveSpeed); Validate();
        ApplyCeiling(ref acceleration, nextPos); Validate();
        ApplyCreatures(ref acceleration, entity, nextPos); Validate();
        ApplyGeneralFriction(ref acceleration, effectiveVelocity); Validate();
        ApplyFloorFriction(ref acceleration, elapsedTime, state, effectiveVelocity, dirCollisionToMe); Validate();
        velocityComp.Value = effectiveVelocity + acceleration * elapsedTime;
        ApplyJumps(ref velocityComp, ref controls);
        ApplySpeedLimit(ref velocityComp, in state, invFairy.moveSpeed); Validate();

        location.LookIn(location.InnerForward with { Y = 0f });
        Debug.Assert(Math.Abs(location.InnerRight.Y) < 0.0001f);

        void Validate() =>
            ValidateState(acceleration, nextPos, effectiveVelocity, dirCollisionToMe);
    }

    [Conditional("DEBUG")]
    private static void ValidateState(
        in Vector3 acceleration,
        in Vector3 nextPos,
        in Vector3 effectiveVelocity,
        in Vector3 dirCollisionToMe)
    {
        Debug.Assert(acceleration.IsFinite());
        Debug.Assert(nextPos.IsFinite());
        Debug.Assert(effectiveVelocity.IsFinite());
        Debug.Assert(dirCollisionToMe.IsFinite());
    }

    private void MicroStep(
        ref components.FairyPhysics state,
        ref Vector3 nextPos,
        ref Vector3 effectiveVelocity,
        Vector3 startPos,
        float elapsedStepTime,
        float colliderRadius,
        ref Vector3 dirCollisionToMe)
    {
        var prevPos = nextPos;
        nextPos += effectiveVelocity * elapsedStepTime;

        Intersection? intersection = null;
        float bestIntersectionDistSqr = float.PositiveInfinity;
        foreach (var curIntersection in worldCollider.Intersections(new Sphere(nextPos, colliderRadius)))
        {
            if (Vector3.Dot(curIntersection.Normal, effectiveVelocity) >= 0f)
                continue;
            var curDistSqr = Vector3.DistanceSquared(nextPos, curIntersection.Point);
            if (curDistSqr < bestIntersectionDistSqr)
            {
                bestIntersectionDistSqr = curDistSqr;
                intersection = curIntersection;
            }
        }
        if (intersection is null)
            return;
        state.HitCeiling = true;
        dirCollisionToMe = MathEx.SafeNormalize(nextPos - intersection.Value.Point);
        if (dirCollisionToMe.Y > MinFloorYDir)
            state.HitFloor = true;

        float angleCollVel = Vector3.Dot(dirCollisionToMe, effectiveVelocity);
        if (MathEx.CmpZero(angleCollVel))
            effectiveVelocity += dirCollisionToMe * MinCollisionBounce;
        else
            effectiveVelocity += dirCollisionToMe * Math.Abs(angleCollVel) * CollisionBounceFactor;
        nextPos = prevPos;
    }

    private void ApplyControls(
        ref Vector3 acceleration,
        ref components.FairyPhysics state,
        in components.PlayerControls controls,
        Location location,
        float moveSpeed)
    {
        if (controls.GoesForward)
            acceleration += location.InnerForward * moveSpeed * moveSpeed * SpeedForward;
        else if (controls.GoesBackward)
            acceleration += location.InnerForward * moveSpeed * moveSpeed * SpeedBackward;

        if (controls.GoesRight)
            acceleration -= location.InnerRight * moveSpeed * moveSpeed * SpeedSideways;
        else if (controls.GoesLeft)
            acceleration += location.InnerRight * moveSpeed * moveSpeed * SpeedSideways;

        state.IsRunning = controls.GoesAnywhere;
    }

    private void ApplyCeiling(
        ref Vector3 acceleration,
        Vector3 myPos)
    {
        if (ceiling is null)
            return;
        var distanceToCeiling = ceiling.pos.Y - myPos.Y;
        if (distanceToCeiling > MaxCeilingDistance)
            return;
        acceleration.Y += CeilingForce / Math.Max(MinCeilingDistance, distanceToCeiling);
    }

    private void ApplyCreatures(
        ref Vector3 acceleration,
        DefaultEcs.Entity meFairy,
        Vector3 myPos)
    {
        foreach (var otherFairy in fairySet.GetEntities())
        {
            if (otherFairy == meFairy)
                continue;
            var fairyToMe = otherFairy.Get<Location>().LocalPosition - myPos;
            var colliderRadius = otherFairy.Get<Sphere>().Radius * CreatureRadiusFactor;
            var distance = fairyToMe.Length();
            if (distance > MinCreatureDistance && distance < colliderRadius)
                acceleration += fairyToMe * (colliderRadius + 1f / distance) / distance;
        }
    }

    private void ApplyGeneralFriction(
        ref Vector3 acceleration,
        Vector3 effectiveVelocity)
    {
        acceleration -= effectiveVelocity * MLFriction;
    }

    private void ApplyFloorFriction(
        ref Vector3 acceleration,
        float elapsedTime,
        in components.FairyPhysics state,
        Vector3 effectiveVelocity,
        Vector3 dirCollisionToMe)
    {
        if (!state.HitFloor)
            return;
        if (MathEx.CmpZero(dirCollisionToMe.LengthSquared()))
            throw new InvalidOperationException("This should not have happened");
        var accInCollision = MathEx.Project(acceleration, dirCollisionToMe);
        Debug.Assert(accInCollision.IsFinite());

        if (MathEx.CmpZero(effectiveVelocity.LengthSquared()))
        {
            // enough collision to come to a complete stop
            if ((acceleration + accInCollision).LengthSquared() > MLFriction * MLFriction)
                acceleration = Vector3.Zero;
            return;
        }

        var velInCollision = effectiveVelocity - MathEx.Project(effectiveVelocity, dirCollisionToMe);
        Debug.Assert(velInCollision.IsFinite());

        float friction = state.IsRunning ? 0f
            : Vector3.Dot(dirCollisionToMe, acceleration) * MFriction * MFrictionFactor * -1;
        var frictionSpeedSqr = friction * friction * elapsedTime * elapsedTime;
        var frictionDir = -MathEx.SafeNormalize(velInCollision);
        Debug.Assert(frictionDir.IsFinite());
        Debug.Assert(float.IsFinite(frictionSpeedSqr));

        if (velInCollision.LengthSquared() >= frictionSpeedSqr) // is this FPS-dependent due to pow?
        {
            if (MathEx.CmpZero(frictionSpeedSqr))
                return;
            acceleration += frictionDir * friction;
        }
        else
        {
            // at this point I have no clue what is happening, I should investigate 
            // what this semantically does and why it is so complicated
            // it also seems like it could be reduced quite a bit
            var frictionDivider = friction * elapsedTime;
            acceleration += frictionDir * friction * velInCollision.Length() / frictionDivider;
            Debug.Assert(acceleration.IsFinite());
        }
    }

    private void ApplyJumps(
        ref components.Velocity velocityComp,
        ref components.PlayerControls controls)
    {
        if (!isInterior && controls.Jumps &&
            velocityComp.Value.Y < MaxJumpVelocity)
        {
            velocityComp.Value += Vector3.UnitY * JumpVelocity;
            controls.Jumps = false; // TODO: CHECK THIS, this is not original
        }
    }

    private void ApplySpeedLimit(
        ref components.Velocity velocityComp,
        in components.FairyPhysics state,
        float moveSpeed)
    {
        var velocity = velocityComp.Value;
        if (state.HitFloor && velocity.Y > -FloorDejitterBand && velocity.Y < 0f)
            velocity.Y = -FloorDejitterBand;
        
        var horSpeed = (velocity with { Y = 0f }).Length();
        var maxHorSpeed = Math.Abs(MaxSpeedFactor * moveSpeed);
        if (!MathEx.CmpZero(horSpeed) && horSpeed > maxHorSpeed)
        {
            velocity.X *= maxHorSpeed / horSpeed;
            velocity.Z *= maxHorSpeed / horSpeed;
        }

        velocityComp.Value = velocity;
    }
}
