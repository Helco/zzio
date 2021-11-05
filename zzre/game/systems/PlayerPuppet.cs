using System;
using System.Numerics;
using DefaultEcs.System;
using zzre.rendering;
using AnimationState = zzre.game.components.HumanPhysics.AnimationState;

namespace zzre.game.systems
{
    public partial class PlayerPuppet : AEntitySetSystem<float>
    {
        private const float MaxFallTime = 0.9f;
        private const float MaxWhirlFallTime = MaxFallTime * 3f;
        private const float SmallFallTime = 0.23f;
        private const float BigFallTime = 0.31f;
        private const float SmallControlLockTime = 0.1f;
        private const float BigControlLockTime = 0.4f;
        private const float MinFallAnimationTime = 0.3f;
        private const float CameraForwardYFactor = -0.7f;
        private const string ThudVoiceSampleBase = "resources/AUDIO/SFX/VOICES/AMY/THD00";
        private const string ThudVoiceSample1 = ThudVoiceSampleBase + "A.WAV";
        private const string ThudVoiceSample2 = ThudVoiceSampleBase + "B.WAV";

        private readonly Camera camera;

        public PlayerPuppet(ITagContainer diContainer) : base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, null, 0)
        {
            camera = diContainer.GetTag<Camera>();
        }

        [Update]
        private void Update(
            float elapsedTime,
            ref components.PlayerPuppet puppet,
            ref components.HumanPhysics physics,
            ref components.NonFairyAnimation animation,
            in components.ActorParts actorParts)
        {
            if (physics.IsDrowning)
                Console.WriteLine("Player died of drowning"); // TODO: Add player death caused by drowning

            Animation(elapsedTime, ref puppet, physics, ref animation);
            Falling(elapsedTime, ref puppet, physics, ref animation);
            // TODO: Add player idle behavior (voice and horizontal velocity)
            // TODO: Add NPC comfort zone
            ActorTargetDirection(physics, actorParts);
        }

        private void Animation(
            float elapsedTime,
            ref components.PlayerPuppet puppet,
            in components.HumanPhysics physics,
            ref components.NonFairyAnimation animation)
        {
            var newAnimation = physics.State switch
            {
                AnimationState.Idle => zzio.AnimationType.Idle0,
                AnimationState.Walk => zzio.AnimationType.Walk0,
                AnimationState.Run => zzio.AnimationType.Run,
                AnimationState.Jump => zzio.AnimationType.Jump,
                AnimationState.Fall => zzio.AnimationType.Fall,
                _ => throw new NotSupportedException($"Unimplemented animation for physics state {physics.State}")
            };

            // there was a timer for the fall rotation originally, but it seems to also have been overridden
            if (newAnimation != zzio.AnimationType.Fall || animation.Next == zzio.AnimationType.Jump)
                animation.Next = newAnimation;
        }

        private void Falling(
            float elapsedTime,
            ref components.PlayerPuppet puppet,
            in components.HumanPhysics physics,
            ref components.NonFairyAnimation animation)
        {
            float prevFallTimer = puppet.FallTimer; // as hitting the floor resets the timer
            if (physics.State == AnimationState.Fall)
            {
                puppet.FallTimer += elapsedTime;
                var maxFallTime = physics.GravityModifier < 0f ? MaxWhirlFallTime : MaxFallTime;
                if (puppet.FallTimer > maxFallTime)
                    Console.WriteLine("Player died because of fall"); // TODO: Add player death based on fall time
            }
            else
                puppet.FallTimer = 0f;

            if (!physics.HitFloor || prevFallTimer < SmallFallTime)
                return;

            float lockControlsFor;
            string? voiceSample;
            if (prevFallTimer < BigFallTime)
            {
                lockControlsFor = SmallControlLockTime;
                voiceSample = GlobalRandom.Get.NextFloat() switch
                {
                    var v when v > 0.8f => ThudVoiceSample1,
                    var v when v > 0.6f => ThudVoiceSample2,
                    _ => null
                };
            }
            else
            {
                animation.Next = zzio.AnimationType.ThudGround;
                lockControlsFor = BigControlLockTime;
                voiceSample = GlobalRandom.Get.NextFloat() switch
                {
                    var v when v > 0.6f => ThudVoiceSample1,
                    var v when v > 0.3f => ThudVoiceSample2,
                    _ => null
                };
            }

            // TODO: Add control locking because of falls
            // TODO: Add voice sample for falls
            // TODO: Add force footstep sound for falls
            Console.WriteLine($"Player fell, cannot move for {lockControlsFor} and says {voiceSample}");
            puppet.DidResetPlanarVelocity = true; // but why?
        }

        private void ActorTargetDirection(
            in components.HumanPhysics physics,
            in components.ActorParts actorParts)
        {
            // originally an addition, but that bug is too rare to implement
            if (MathEx.CmpZero(physics.Velocity.X) && MathEx.CmpZero(physics.Velocity.Z))
                return;

            var targetDir = physics.Velocity;
            if (physics.HitFloor)
                targetDir.Y = 0f;
            targetDir = Vector3.Normalize(targetDir);

            targetDir.Y = camera.Location.InnerForward.Y * CameraForwardYFactor;
            ref var actorMovement = ref actorParts.Body.Get<components.PuppetActorMovement>();
            actorMovement.TargetDirection = Vector3.Normalize(targetDir);
        }
    }
}
