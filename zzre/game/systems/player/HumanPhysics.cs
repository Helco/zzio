using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using DefaultEcs.System;
using zzre.rendering;
using AnimationState = zzre.game.components.HumanPhysics.AnimationState;

namespace zzre.game.systems;

public partial class HumanPhysics : AEntitySetSystem<float>
{
    private enum CollisionType
    {
        None,
        World,
        Model,
        Creature
    }

    private struct Collision
    {
        public readonly Vector3 point;
        public readonly Vector3 normal;
        public readonly CollisionType type;
        public Vector3 dirToPlayer;

        public Collision(Intersection intersection, CollisionType type)
        {
            (point, normal) = (intersection.Point, intersection.Normal);
            this.type = type;
            dirToPlayer = Vector3.Zero;
        }
    }

    private readonly Random Random = Random.Shared;
    private readonly IDisposable configDisposable;
    private readonly IDisposable sceneLoadedSubscription;
    private readonly IDisposable controlsLockedSubscription;
    private readonly DefaultEcs.EntitySet collidableModels;
    private readonly DefaultEcs.EntitySet collidableCreatures;
    private bool isInterior;
    private WorldCollider worldCollider = null!;

    public HumanPhysics(ITagContainer diContainer) : base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, useBuffer: true)
    {
        World.SetMaxCapacity<components.HumanPhysics>(1);
        configDisposable = diContainer.GetConfigFor(this);
        sceneLoadedSubscription = World.Subscribe<messages.SceneLoaded>(HandleSceneLoaded);
        controlsLockedSubscription = World.Subscribe<messages.LockPlayerControl>(HandleControlsLocked);

        collidableModels = World
            .GetEntities()
            .With<IIntersectionable>()
            .With<components.Collidable>()
            .With<ClumpMesh>() // a model not a creature
            .AsSet();
        collidableCreatures = World
            .GetEntities()
            .With<components.Collidable>()
            .With<components.ActorParts>() // the player is not collidable, don't worry
            .AsSet();
    }

    public override void Dispose()
    {
        base.Dispose();
        configDisposable.Dispose();
        sceneLoadedSubscription.Dispose();
        controlsLockedSubscription.Dispose();
        collidableModels.Dispose();
        collidableCreatures.Dispose();
    }

    private void HandleSceneLoaded(in messages.SceneLoaded message)
    {
        worldCollider = World.Get<WorldCollider>();
        isInterior = message.Scene.dataset.isInterior;
    }

    private void HandleControlsLocked(in messages.LockPlayerControl message)
    {
        if (!message.MovingForward)
            return;
        foreach (var entity in Set.GetEntities())
        {
            ref var physics = ref entity.Get<components.HumanPhysics>();
            physics.DisableModelCollisionTimer = message.Duration;
            physics.Velocity = Vector3.Zero;
        }
    }

    [Update]
    private void Update(float elapsedTime,
        Location location,
        ref components.HumanPhysics state,
        ref components.PlayerControls controls)
    {
        if (elapsedTime > MaxElapsedTime)
        {
            state.State = AnimationState.Idle;
            return;
        }

        state.DisableModelCollisionTimer = Math.Max(0f, state.DisableModelCollisionTimer - elapsedTime);
        
        float oldSpeedModifier = state.SpeedModifier;
        float quarterColliderSize = state.ColliderSize * 0.25f;
        Collision collision = default;
        state.HitCeiling = state.HitFloor = false;
        state.State = AnimationState.Idle;

        var stepCount = state.Velocity.Length() / (quarterColliderSize * quarterColliderSize) + 1;
        var elapsedStepTime = elapsedTime / stepCount;
        var newPos = location.LocalPosition;
        for (int i = 0; i < stepCount; i++)
            newPos = MicroStep(elapsedStepTime, quarterColliderSize, newPos, location.InnerForward, controls, ref state, ref collision);

        if (state.HitCeiling && state.Velocity.Y > 0f)
            state.Velocity.Y = 0f;

        ForcedJump(newPos, ref controls, ref state, collision);
        IntentionalJump(ref controls, ref state);
        WhirlJump(ref controls, ref state);
        // TODO: Add human physics water handling

        if (!state.HitFloor)
            state.State = state.Velocity.Y <= 0f ? AnimationState.Fall : AnimationState.Jump;

        ApplyGravity(elapsedTime, ref state);

        state.SpeedModifier = oldSpeedModifier;
        location.LocalPosition = newPos;
    }

    private Vector3 MicroStep(
        float elapsedStepTime,
        float quarterColliderSize,
        in Vector3 oldPos,
        in Vector3 forward,
        in components.PlayerControls controls,
        ref components.HumanPhysics state,
        ref Collision collision)
    {
        var newPos = oldPos + state.Velocity * elapsedStepTime;

        newPos = WorldAndModelCollision(quarterColliderSize, quarterColliderSize, newPos, ref state, ref collision);
        newPos = WorldAndModelCollision(-quarterColliderSize, quarterColliderSize, newPos, ref state, ref collision);
        newPos = CreatureCollision(newPos);

        var velocityAngle = Vector3.Dot(state.Velocity, forward);
        var mainVelocity = forward * velocityAngle;
        var slipVelocity = state.Velocity - mainVelocity;

        ApplyControls(elapsedStepTime, ref mainVelocity, forward, controls, ref state, in collision);
        if (state.SpeedModifier < MinRunSpeed && state.State == AnimationState.Run)
            state.State = AnimationState.Walk;

        if (UseWorldForces)
        {
            mainVelocity *= MathF.Pow(MoveFriction, elapsedStepTime);
            slipVelocity *= MathF.Pow(SlipFriction, elapsedStepTime);
        }
        state.Velocity = mainVelocity + slipVelocity;
        return newPos;
    }

    private Vector3 WorldAndModelCollision(
        float colliderOffset,
        float colliderRadius,
        Vector3 newPos,
        ref components.HumanPhysics state,
        ref Collision collision)
    {
        newPos += Vector3.UnitY * colliderOffset;
        var collider = new Sphere(newPos, colliderRadius);

        var velocity = state.Velocity;
        var intersections = FindAllIntersections(collider, state.DisableModelCollisionTimer <= 0f)
            .Where(i => Vector3.Dot(velocity, i.normal) < 0f);
        if (!intersections.Any())
            return newPos - colliderOffset * Vector3.UnitY;
        collision = intersections.MinBy(i => Vector3.DistanceSquared(i.point, newPos));

        collision.dirToPlayer = newPos - collision.point;
        if (colliderOffset > 0f) // only for the upper collider check
        {
            state.HitCeiling = collision.dirToPlayer.Y < 0f;
            collision.dirToPlayer.Y = Math.Max(0f, collision.dirToPlayer.Y);
        }
        collision.dirToPlayer = MathEx.SafeNormalize(collision.dirToPlayer);
        state.HitFloor |= collision.dirToPlayer.Y > MinFloorYDir;

        if (PreserveVelocityAtCollision)
        {
            if (collision.dirToPlayer.Y > MaxCollisionYDir)
                collision.dirToPlayer = Vector3.UnitY;
            else
            {
                collision.dirToPlayer.Y = 0f;
                collision.dirToPlayer = MathEx.SafeNormalize(collision.dirToPlayer);
            }
        }

        var collisionDistance = Vector3.Distance(collision.point, newPos);
        newPos += collision.dirToPlayer * (colliderRadius - collisionDistance);

        if (!PreserveVelocityAtCollision)
        {
            var impactAngle = Vector3.Dot(state.Velocity, collision.dirToPlayer);
            if (impactAngle < 0f)
                state.Velocity -= impactAngle * collision.dirToPlayer;
        }

        return newPos - colliderOffset * Vector3.UnitY;
    }

    private Vector3 CreatureCollision(Vector3 newPos)
    {
        var intersection = FindCreatureIntersection(newPos);
        if (intersection == null)
            return newPos;

        var collisionToPlayer = Vector3.Normalize(newPos - intersection.Value);
        return intersection.Value + collisionToPlayer * CreatureRadius;
    }

    private Vector3? FindCreatureIntersection(Vector3 playerPos)
    {
        // basically a axis-aligned capsule intersection test
        // which is degenerated to a cylinder...

        Vector3? bestPos = default;
        float bestDistanceSqr = CreatureRadius * CreatureRadius;
        foreach (var creature in collidableCreatures.GetEntities())
        {
            var comparePos = creature.Get<Location>().LocalPosition;
            if (creature.TryGet<components.NPCType>() == components.NPCType.PlantBlocker)
                comparePos += Vector3.Normalize(playerPos - comparePos) * PlantBlockerAddRadius;

            if (Math.Abs(comparePos.Y - playerPos.Y) < CreatureHalfHeight)
                comparePos.Y = playerPos.Y;

            var currentDistance = Vector3.DistanceSquared(comparePos, playerPos);
            if (currentDistance < bestDistanceSqr)
            {
                bestDistanceSqr = currentDistance;
                bestPos = comparePos;
            }
        }
        return bestPos;
    }

    private IEnumerable<Collision> FindAllIntersections(Sphere collider, bool canCollideWithModels)
    {
        var intersections = worldCollider
            .Intersections(collider)
            .Select(i => new Collision(i, CollisionType.World));
        if (canCollideWithModels)
            foreach (ref readonly var model in collidableModels.GetEntities())
                intersections = intersections.Concat(model.Get<IIntersectionable>()
                    .Intersections(collider)
                    .Select(i => new Collision(i, CollisionType.Model)));
        return intersections;
    }

    private void ApplyControls(
        float elapsedStepTime,
        ref Vector3 mainVelocity,
        in Vector3 forward,
        in components.PlayerControls controls,
        ref components.HumanPhysics state,
        in Collision collision)
    {
        var foreAftMove = elapsedStepTime * state.SpeedModifier * (
            controls.GoesBackward ? SpeedBackward
            : controls.GoesForward ? SpeedForward
            : 0f);
        if (controls.GoesForward || controls.GoesBackward)
        {
            state.State = AnimationState.Run;
            mainVelocity += forward * foreAftMove;
        }
        if (state.HitFloor && !MathEx.Cmp(collision.dirToPlayer.Y, 1f) && state.State == AnimationState.Run)
        {
            state.State = AnimationState.Walk;
            state.SpeedModifier *= SpeedFallFactor;
        }

        if (controls.GoesRight)
            ApplySideControls(elapsedStepTime, ref mainVelocity, new Vector3(forward.Z, forward.Y, -forward.X), ref state);
        else if (controls.GoesLeft)
            ApplySideControls(elapsedStepTime, ref mainVelocity, new Vector3(-forward.Z, forward.Y, forward.X), ref state);
    }

    private void ApplySideControls(
        float elapsedStepTime,
        ref Vector3 mainVelocity,
        Vector3 axis,
        ref components.HumanPhysics state)
    {
        state.State = AnimationState.Run;
        var controlAngle = Vector3.Dot(state.Velocity, axis);
        if (Math.Abs(controlAngle) < MaxSideControlAngle)
            mainVelocity -= axis * elapsedStepTime * SpeedSide * state.SpeedModifier;
    }

    private void ForcedJump(
        in Vector3 pos,
        ref components.PlayerControls controls,
        ref components.HumanPhysics state,
        in Collision collision)
    {
        // yes obnoxiously preconditions are in place to stop forced jumps, it will still happen
        var horizontalVelocity = state.Velocity * new Vector3(1f, 0f, 1f);
        if (horizontalVelocity.LengthSquared() >= MathF.Pow(MaxForcedJumpSpeed, 2f)
            || controls.GoesAnywhere
            || controls.Jumps
            || controls.WhirlJumps
            || collision.type != CollisionType.World
            || !state.HitFloor
            || worldCollider.Cast(new Line(
                pos + Vector3.UnitY * 1f,
                pos - Vector3.UnitY * 1.6f)) != null)
            return;

        var jumpDir = pos - collision.point;
        jumpDir.Y = 0.001f;
        jumpDir = Vector3.Normalize(jumpDir);
        state.Velocity.X = jumpDir.X * SpeedForcedJump;
        state.Velocity.Z = jumpDir.Z * SpeedForcedJump;
        state.SpeedModifier = SpeedFactorForcedJump;
        controls.Jumps = true;
    }

    private static readonly IReadOnlyList<string> JumpVoiceSamples =
    [
        "resources/audio/sfx/voices/amy/jmp00a.wav",
        "resources/audio/sfx/voices/amy/jmp00b.wav"
    ];
    private void IntentionalJump(
        ref components.PlayerControls controls,
        ref components.HumanPhysics state)
    {
        if (!controls.Jumps
            || isInterior
            || (!state.HitFloor && !CanJumpWithoutFloor))
            return;

        controls.Jumps = false;
        state.HitFloor = false;
        if (state.SpeedModifier < MinRunSpeed)
        {
            state.Velocity *= SpeedFactorSmallJump;
            state.Velocity.Y = SpeedJump * SpeedFactorSmallJump;
        }
        else
        {
            state.Velocity *= SpeedFactorBigJump;
            state.Velocity.Y = SpeedJump;

            var voiceSample = Random.NextDouble() switch
            {
                >= 0.6 => JumpVoiceSamples[0],
                >= 0.3 => JumpVoiceSamples[1],
                _ => null
            };
            if (voiceSample is not null)
                World.Publish(new messages.SpawnSample(voiceSample));
        }
    }

    private void WhirlJump(
        ref components.PlayerControls controls,
        ref components.HumanPhysics state)
    {
        if (!controls.WhirlJumps)
            return;
        controls.WhirlJumps = false;
        state.GravityModifier = -1f;
        state.Velocity.Y = SpeedJump * SpeedFactorWhirlJump;
        World.Publish(new messages.SpawnSample("resources/audio/sfx/voices/amy/jms00a.wav"));
    }

    private void ApplyGravity(
        float elapsedTime,
        ref components.HumanPhysics state)
    {
        if (!UseWorldForces)
            return;
        if (state.HitFloor)
        {
            state.GravityModifier = 1f;
            return;
        }

        if (state.GravityModifier <= 0f)
            state.Velocity.Y -= elapsedTime * Gravity * WhirlJumpGravityFactor;
        else
        {
            state.Velocity.Y -= elapsedTime * Gravity * state.GravityModifier;
            state.GravityModifier += elapsedTime * GravityModifierSpeed;
        }
    }
}
