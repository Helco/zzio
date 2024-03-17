using System;
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
    [Configuration(Description = "An additiona factor for the floor sliding friction")]
    private float MFrictionFactor = 1.5f;

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

        int stepCount = (int)(1 + startSpeed / colliderSphere.RadiusSq);
        stepCount = Math.Min(stepCount, MaxMicroSteps);
        var elapsedStepTime = elapsedTime / stepCount;
        var nextPos = startPos;
        var effectiveVelocity = startVelocity; // the velocity after micro steps
        for (int i = 0; i < stepCount; i++)
        {
            if (Vector3.DistanceSquared(nextPos, startPos) > maxMoveDistanceSqr)
                break;
            MicroStep(ref state, ref nextPos, ref effectiveVelocity, startPos, elapsedStepTime, colliderSphere.Radius, out dirCollisionToMe);
        }
        var effectiveSpeed = effectiveVelocity.Length();
        location.LocalPosition = nextPos;

        var acceleration = Vector3.UnitY * -Gravity;
        ApplyControls(ref acceleration, ref state, controls, location, invFairy.moveSpeed);
        ApplyCeiling(ref acceleration, nextPos);
        ApplyCreatures(ref acceleration, entity, nextPos);
        ApplyGeneralFriction(ref acceleration, effectiveVelocity);
        ApplyFloorFriction(ref acceleration, elapsedTime, state, effectiveVelocity, dirCollisionToMe);
        velocityComp.Value += acceleration * elapsedTime;
    }

    private void MicroStep(
        ref components.FairyPhysics state,
        ref Vector3 nextPos,
        ref Vector3 effectiveVelocity,
        Vector3 startPos,
        float elapsedStepTime,
        float colliderRadius,
        out Vector3 dirCollisionToMe)
    {
        dirCollisionToMe = Vector3.Zero;
        nextPos += effectiveVelocity * elapsedStepTime;

        Intersection? intersection = null;
        float bestIntersectionDistSqr = float.PositiveInfinity;
        foreach (var curIntersection in worldCollider.Intersections(new Sphere(nextPos, colliderRadius)))
        {
            if (Vector3.Dot(curIntersection.Normal, effectiveVelocity) < 0f)
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
        dirCollisionToMe = Vector3.Normalize(nextPos - intersection.Value.Point);
        if (dirCollisionToMe.Y > MinFloorYDir)
            state.HitFloor = true;

        float angleCollVel = Vector3.Dot(dirCollisionToMe, effectiveVelocity);
        if (MathEx.CmpZero(angleCollVel))
            effectiveVelocity += dirCollisionToMe * MinCollisionBounce;
        else
            effectiveVelocity -= dirCollisionToMe * Math.Sign(angleCollVel) * CollisionBounceFactor;
    }

    private void ApplyControls(
        ref Vector3 acceleration,
        ref components.FairyPhysics state,
        in components.PlayerControls controls,
        Location location,
        float moveSpeed)
    {
        if (controls.GoesForward)
            acceleration += location.InnerForward * moveSpeed * SpeedForward;
        else if (controls.GoesBackward)
            acceleration += location.InnerForward * moveSpeed * SpeedBackward;

        if (controls.GoesRight)
            acceleration -= location.InnerRight * moveSpeed * SpeedSideways;
        else if (controls.GoesLeft)
            acceleration += location.InnerRight * moveSpeed * SpeedSideways;

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
        var accInCollision = MathEx.Project(acceleration, dirCollisionToMe);
        var velInCollision = MathEx.Project(effectiveVelocity, dirCollisionToMe);

        if (MathEx.CmpZero(effectiveVelocity.LengthSquared()))
        {
            // enough collision to come to a complete stop
            if ((acceleration + accInCollision).LengthSquared() > MLFriction * MLFriction)
                acceleration = Vector3.Zero;
            return;
        }

        float friction = Vector3.Dot(dirCollisionToMe, acceleration) * MFriction * MFrictionFactor;
        var frictionSpeedSqr = friction * friction * elapsedTime * elapsedTime;
        var frictionDir = -MathEx.SafeNormalize(velInCollision);
        if (state.IsRunning)
            friction = 0f;

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
        }
    }
}
