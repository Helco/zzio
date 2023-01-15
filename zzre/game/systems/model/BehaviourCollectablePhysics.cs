namespace zzre.game.systems;
using System;
using System.Numerics;
using DefaultEcs.System;

public partial class BehaviourCollectablePhysics : AEntitySetSystem<float>
{
    private const float MinVelocityY = 1.2f;
    private const float RangeVelocityY = 0.3f;
    private const float VelocityFactor = 2f;
    private const float CameraDirFactor = 0.15f;
    private const float RandomDirFactor = 0.1f;
    private const float MaxAge = 20f;
    private const float BlinkingAge = 19f;
    private const float YOffset = 0.5f;
    private const float Gravity = 3.1f;
    private const float BonkSpeed = 0.5f;
    private const float BonkYSpeed = -0.6f;
    private const float BonkDistance = 0.2f;
    private const float MinBonkYSpeed = 0.3f;

    private readonly rendering.Camera camera;
    private readonly IDisposable addedSubscription;
    private WorldCollider? worldCollider;

    public BehaviourCollectablePhysics(ITagContainer diContainer) : base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, useBuffer: false)
    {
        camera = diContainer.GetTag<rendering.Camera>();
        addedSubscription = World.SubscribeComponentAdded<components.behaviour.CollectablePhysics>(HandleComponentAdded);
    }

    public override void Dispose()
    {
        base.Dispose();
        addedSubscription.Dispose();
    }

    private void HandleComponentAdded(in DefaultEcs.Entity entity, in components.behaviour.CollectablePhysics physics)
    {
        var random = Random.Shared;
        var cameraDir = camera.Location.InnerForward;

        entity.Set(physics with
        {
            Velocity = Vector3.Normalize(
                (random.InCube() * RandomDirFactor + cameraDir * CameraDirFactor) with { Y = 0f }),
            YVelocity = random.NextFloat() * RangeVelocityY + MinVelocityY
        });
    }

    [Update]
    private void Update(
        float elapsedTime,
        in DefaultEcs.Entity entity,
        Location location,
        ref components.behaviour.Collectable collectable,
        ref components.behaviour.CollectablePhysics physics)
    {
        Aging(elapsedTime, entity, ref collectable);
        if (MathEx.CmpZero(physics.YVelocity))
            return;

        var oldPos = location.LocalPosition;
        var curVelocity = VelocityFactor * physics.Velocity with { Y = physics.YVelocity };
        var newPos = oldPos + elapsedTime * curVelocity - Vector3.UnitY * YOffset;
        physics.YVelocity -= elapsedTime * Gravity;

        worldCollider ??= World.Get<WorldCollider>();
        var intersection = worldCollider.Cast(new Line(oldPos, newPos));
        if (intersection.HasValue)
        {
            physics.Velocity = Vector3.Normalize(intersection.Value.Normal) * BonkSpeed;
            physics.YVelocity *= BonkYSpeed;
            if (Math.Abs(physics.YVelocity) < MinBonkYSpeed)
                physics.YVelocity = 0f; // stopping any jumps

            newPos = intersection.Value.Point + physics.Velocity * BonkDistance;
        }
        location.LocalPosition = newPos + Vector3.UnitY * YOffset;
    }

    private static void Aging(
        float elapsedTime,
        DefaultEcs.Entity entity,
        ref components.behaviour.Collectable collectable)
    {
        if (collectable.IsDying)
            return;

        collectable.Age += elapsedTime;
        if (collectable.Age > MaxAge)
            entity.Set<components.Dead>();
        if (collectable.Age > BlinkingAge)
        {
            // TODO: Fix FPS dep. collectable blinking
            if (entity.Has<components.Visibility>())
                entity.Remove<components.Visibility>();
            else
                entity.Set<components.Visibility>();
        }
    }
}
