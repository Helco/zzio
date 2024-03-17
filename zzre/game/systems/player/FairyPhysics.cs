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


    private readonly IDisposable configDisposable;
    private readonly IDisposable sceneLoadedSubscription;
    private readonly DefaultEcs.EntitySet fairySet;
    private WorldCollider worldCollider = null!;
    private zzio.scn.Trigger? ceiling;

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
    }

    [Update]
    private void Update(float elapsedTime,
        Location location,
        ref components.FairyPhysics state,
        ref components.PlayerControls controls,
        ref components.Velocity velocityComp,
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
            if (Vector3.Dot(curIntersection.Point, effectiveVelocity) < 0f)
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
}
