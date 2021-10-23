using System;
using System.Linq;
using System.Numerics;
using DefaultEcs.System;
using AnimationState = zzre.game.components.HumanPhysics.AnimationState;

namespace zzre.game.systems
{
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

        private readonly WorldCollider worldCollider;
        private readonly bool isInterior;

        public HumanPhysics(ITagContainer diContainer) : base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, null, 0)
        {
            World.SetMaxCapacity<components.HumanPhysics>(1);
            worldCollider = diContainer.GetTag<WorldCollider>();

            var scene = diContainer.GetTag<zzio.scn.Scene>();
            isInterior = scene.dataset.isInterior;
        }

        [Update]
        private void Update(float elapsedTime,
            Location location,
            in components.PhysicParameters parameters,
            ref components.HumanPhysics state,
            ref components.PlayerControls controls)
        {
            if (elapsedTime > parameters.MaxElapsedTime)
            {
                state.State = AnimationState.Idle;
                return;
            }

            float oldSpeedModifier = state.SpeedModifier;
            float quarterColliderSize = state.ColliderSize * 0.25f;
            Collision collision = default;
            state.HitCeiling = state.HitFloor = false;

            var stepCount = state.Velocity.Length() / (quarterColliderSize * quarterColliderSize) + 1;
            var elapsedStepTime = elapsedTime / stepCount;
            var newPos = location.LocalPosition;
            for (int i = 0; i < stepCount; i++)
                newPos = MicroStep(elapsedStepTime, quarterColliderSize, newPos, location.InnerForward, parameters, controls, ref state, ref collision);

            if (state.HitCeiling && state.Velocity.Y > 0f)
                state.Velocity.Y = 0f;

            ForcedJump(newPos, parameters, ref controls, ref state, collision);
            IntentionalJump(parameters, ref controls, ref state);
            WhirlJump(parameters, ref controls, ref state);
            // TODO: Add human physics water handling

            if (!state.HitFloor)
                state.State = state.Velocity.Y <= 0f ? AnimationState.Fall : AnimationState.Jump;

            ApplyGravity(elapsedTime, parameters, ref state);

            state.SpeedModifier = oldSpeedModifier;
            location.LocalPosition = newPos;
        }

        private Vector3 MicroStep(
            float elapsedStepTime,
            float quarterColliderSize,
            in Vector3 oldPos,
            in Vector3 forward,
            in components.PhysicParameters parameters,
            in components.PlayerControls controls,
            ref components.HumanPhysics state,
            ref Collision collision)
        {
            var newPos = oldPos + state.Velocity * elapsedStepTime;

            newPos = WorldAndModelCollision(quarterColliderSize, quarterColliderSize, newPos, parameters, ref state, ref collision);
            newPos = WorldAndModelCollision(-quarterColliderSize, quarterColliderSize, newPos, parameters, ref state, ref collision);
            // TODO: Add player<->creature collision

            var velocityAngle = Vector3.Dot(state.Velocity, forward);
            var mainVelocity = forward * velocityAngle;
            var slipVelocity = state.Velocity - mainVelocity;

            ApplyControls(elapsedStepTime, ref mainVelocity, forward, parameters, controls, ref state, in collision);
            if (state.SpeedModifier < parameters.MinRunSpeed && state.State == AnimationState.Run)
                state.State = AnimationState.Walk;

            if (parameters.UseWorldForces)
            {
                mainVelocity *= MathF.Pow(parameters.MoveFriction, elapsedStepTime);
                slipVelocity *= MathF.Pow(parameters.SlipFriction, elapsedStepTime);
            }
            state.Velocity = mainVelocity + slipVelocity;
            return newPos;
        }

        private Vector3 WorldAndModelCollision(
            float colliderOffset,
            float colliderRadius,
            Vector3 newPos,
            in components.PhysicParameters parameters,
            ref components.HumanPhysics state,
            ref Collision collision)
        {
            newPos += Vector3.UnitY * colliderOffset;
            var collider = new Sphere(newPos, colliderRadius);

            // TODO: Add player<->model collision
            var worldIntersections = worldCollider.Intersections(collider);
            if (!worldIntersections.Any())
                return newPos - colliderOffset * Vector3.UnitY;
            collision = new Collision(worldIntersections
                .OrderBy(i => Vector3.DistanceSquared(i.Point, newPos))
                .First(),
                CollisionType.World);

            collision.dirToPlayer = newPos - collision.point;
            if (colliderOffset > 0f) // only for the upper collider check
            {
                state.HitCeiling = collision.dirToPlayer.Y < 0f;
                collision.dirToPlayer.Y = Math.Max(0f, collision.dirToPlayer.Y);
            }
            collision.dirToPlayer = Vector3.Normalize(collision.dirToPlayer);
            if (collision.dirToPlayer.Y > parameters.MinFloorYDir)
                state.HitFloor = true;

            if (parameters.PreserveVelocityAtCollision)
            {
                if (collision.dirToPlayer.Y > parameters.MaxCollisionYDir)
                    collision.dirToPlayer = Vector3.UnitY;
                else
                {
                    collision.dirToPlayer.Y = 0f;
                    collision.dirToPlayer = Vector3.Normalize(collision.dirToPlayer);
                }
            }

            var collisionDistance = Vector3.Distance(collision.point, newPos);
            newPos += collision.dirToPlayer * (colliderRadius - collisionDistance);

            if (!parameters.PreserveVelocityAtCollision)
            {
                var impactAngle = Vector3.Dot(state.Velocity, collision.dirToPlayer);
                if (impactAngle < 0f)
                    state.Velocity -= impactAngle * collision.dirToPlayer;
            }

            return newPos - colliderOffset * Vector3.UnitY;
        }

        private void ApplyControls(
            float elapsedStepTime,
            ref Vector3 mainVelocity,
            in Vector3 forward,
            in components.PhysicParameters parameters,
            in components.PlayerControls controls,
            ref components.HumanPhysics state,
            in Collision collision)
        {
            var foreAftMove = elapsedStepTime * state.SpeedModifier * (
                controls.GoesBackward ? parameters.SpeedBackward
                : controls.GoesForward ? parameters.SpeedForward
                : 0f);
            if (controls.GoesForward || controls.GoesBackward)
            {
                state.State = AnimationState.Run;
                mainVelocity += forward * foreAftMove;
            }
            if (state.HitFloor && collision.dirToPlayer.Y != 1f && state.State == AnimationState.Run)
            {
                state.State = AnimationState.Walk;
                state.SpeedModifier *= parameters.SpeedFallFactor;
            }

            if (controls.GoesRight)
                ApplySideControls(elapsedStepTime, ref mainVelocity, new Vector3(forward.Z, forward.Y, -forward.X), parameters, ref state);
            else if (controls.GoesLeft)
                ApplySideControls(elapsedStepTime, ref mainVelocity, new Vector3(-forward.Z, forward.Y, forward.X), parameters, ref state);
        }

        private void ApplySideControls(
            float elapsedStepTime,
            ref Vector3 mainVelocity,
            Vector3 axis,
            in components.PhysicParameters parameters,
            ref components.HumanPhysics state)
        {
            state.State = AnimationState.Walk;
            var controlAngle = Vector3.Dot(state.Velocity, axis);
            if (Math.Abs(controlAngle) < parameters.MaxSideControlAngle)
                mainVelocity -= axis * elapsedStepTime * parameters.SpeedSide * state.SpeedModifier;
        }

        private void ForcedJump(
            in Vector3 pos,
            in components.PhysicParameters parameters,
            ref components.PlayerControls controls,
            ref components.HumanPhysics state,
            in Collision collision)
        {
            // yes obnoxiously preconditions are in place to stop forced jumps, it will still happen
            var horizontalVelocity = state.Velocity * new Vector3(1f, 0f, 1f);
            if (horizontalVelocity.LengthSquared() >= MathF.Pow(parameters.MaxForcedJumpSpeed, 2f)
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
            state.Velocity.X = jumpDir.X * parameters.SpeedForcedJump;
            state.Velocity.Z = jumpDir.Z * parameters.SpeedForcedJump;
            state.SpeedModifier = parameters.SpeedFactorForcedJump;
            controls.Jumps = true;
        }

        private void IntentionalJump(
            in components.PhysicParameters parameters,
            ref components.PlayerControls controls,
            ref components.HumanPhysics state)
        {
            if (!controls.Jumps
                || isInterior
                || (!state.HitFloor && !parameters.CanJumpWithoutFloor))
                return;

            controls.Jumps = false;
            state.HitFloor = false;
            if (state.SpeedModifier < parameters.MinRunSpeed)
            {
                state.Velocity *= parameters.SpeedFactorSmallJump;
                state.Velocity.Y = parameters.SpeedJump * parameters.SpeedFactorSmallJump;
            }
            else
            {
                state.Velocity *= parameters.SpeedFactorBigJump;
                state.Velocity.Y = parameters.SpeedJump;

                // TODO: Play voice samples for intentional jumps
            }
        }

        private void WhirlJump(
            in components.PhysicParameters parameters,
            ref components.PlayerControls controls,
            ref components.HumanPhysics state)
        {
            if (!controls.WhirlJumps)
                return;
            controls.WhirlJumps = false;
            state.GravityModifier = -1f;
            state.Velocity.Y = parameters.SpeedJump * parameters.SpeedFactorWhirlJump;

            // TODO: Play voice samples for whirl jumps
        }

        private void ApplyGravity(
            float elapsedTime,
            in components.PhysicParameters parameters,
            ref components.HumanPhysics state)
        {
            if (!parameters.UseWorldForces)
                return;
            if (state.HitFloor)
            {
                state.GravityModifier = 1f;
                return;
            }

            if (state.GravityModifier <= 0f)
                state.Velocity.Y -= elapsedTime * parameters.Gravity * parameters.WhirlJumpGravityFactor;
            else
            {
                state.Velocity.Y -= elapsedTime * parameters.Gravity * state.GravityModifier;
                state.GravityModifier += elapsedTime * parameters.GravityModifierSpeed;
            }
        }
    }
}
