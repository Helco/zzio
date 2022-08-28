using System;
using System.Numerics;
using DefaultEcs.System;
using zzre.rendering;
using AnimationState = zzre.game.components.HumanPhysics.AnimationState;

namespace zzre.game.systems
{
    [PauseDuring(PauseTrigger.UIScreen)]
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
        private const float NPCComfortZoneSpeed = 0.6f;
        private const string ThudVoiceSampleBase = "resources/AUDIO/SFX/VOICES/AMY/THD00";
        private const string ThudVoiceSample1 = ThudVoiceSampleBase + "A.WAV";
        private const string ThudVoiceSample2 = ThudVoiceSampleBase + "B.WAV";

        private readonly Camera camera;

        public PlayerPuppet(ITagContainer diContainer) : base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, useBuffer: true)
        {
            camera = diContainer.GetTag<Camera>();
        }

        [Update]
        private void Update(
            float elapsedTime,
            ref components.PlayerPuppet puppet,
            ref components.HumanPhysics physics,
            ref components.NonFairyAnimation animation,
            ref components.PuppetActorMovement puppetActorMovement)
        {
            if (physics.IsDrowning)
                Console.WriteLine("Player died of drowning"); // TODO: Add player death caused by drowning

            Animation(elapsedTime, ref puppet, physics, ref animation);
            Falling(elapsedTime, ref puppet, ref physics, ref animation);
            Idling(ref puppet, ref physics);
            SpeedModifier(ref physics);
            ActorTargetDirection(physics, ref puppetActorMovement);
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
            ref components.HumanPhysics physics,
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

            // TODO: Add voice sample for falls
            // TODO: Add force footstep sound for falls
            Console.WriteLine($"Player fell, cannot move for {lockControlsFor} and says {voiceSample}");
            World.Publish(new messages.LockPlayerControl(lockControlsFor));

            physics.Velocity *= Vector3.UnitY;
            puppet.DidResetPlanarVelocity = true;
        }

        private void Idling(
            ref components.PlayerPuppet puppet,
            ref components.HumanPhysics physics)
        {
            if (physics.State != AnimationState.Idle)
                puppet.DidResetPlanarVelocity = false;
            else if (!puppet.DidResetPlanarVelocity)
            {
                puppet.DidResetPlanarVelocity = true;
                physics.Velocity *= Vector3.UnitY;
            }
        }

        private void SpeedModifier(ref components.HumanPhysics physics)
        {
            physics.SpeedModifier = World.Has<components.ActiveNPC>()
                ? NPCComfortZoneSpeed
                : World.Get<zzio.scn.Scene>().dataset.isInterior
                ? components.HumanPhysics.InteriorSpeedModifier
                : components.HumanPhysics.ExteriorSpeedModifier;
        }

        private void ActorTargetDirection(
            in components.HumanPhysics physics,
            ref components.PuppetActorMovement puppetActorMovement)
        {
            if (physics.Velocity.X + physics.Velocity.Z == 0f)
                return;

            var targetDir = physics.Velocity with { Y = 0 };
            targetDir = MathEx.SafeNormalize(targetDir);

            targetDir.Y = camera.Location.InnerForward.Y * CameraForwardYFactor;
            puppetActorMovement.TargetDirection = MathEx.SafeNormalize(targetDir);
        }
    }
}
