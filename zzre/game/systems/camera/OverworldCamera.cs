using System;
using System.Numerics;
using DefaultEcs.System;

namespace zzre.game.systems
{
    public class OverworldCamera : BaseGameCamera
    {
        private static readonly Vector2 SpeedFactor = new Vector2(0.1f, 15f);
        private static readonly Vector3 CameraDirectionFactor = new Vector3(1f, 0.2f, 1f);
        private const float HorizontalDeadzone = 0.5f;
        private const float AdditionalHeight = 1f;
        private const float MaxVerAngle = 1.3f;
        private const float MaxCamDistExterior = 3f;
        private const float MaxCamDistInterior = 1.4f;
        private const float ClipDistance = 0.2f;
        private const float BackwardSpeed = 1.5f;
        private const float ForwardFraction = 0.6f; // TODO: Convert camera forward fraction into proper speed
        private const float MinFastLerpDistance = 2.5f;
        private const float FastLerpSpeed = 0.001f;
        private const float SlowLerpSpeed = 0.00001f;
        private const float NearHeightDistance = 0.5f;
        private const float NearHeightFactor = 1f / 5f;

        private readonly float maxCameraDistance;
        private float currentVerAngle;
        private float curCamDistance;

        public OverworldCamera(ITagContainer diContainer) : base(diContainer)
        {
            var scene = diContainer.GetTag<zzio.scn.Scene>();
            maxCameraDistance = scene.dataset.isInterior
                ? MaxCamDistInterior
                : MaxCamDistExterior;
            curCamDistance = maxCameraDistance;
        }

        protected override void Update(float elapsedTime, Vector2 delta)
        {
            delta.X = DeadZone(delta.X, HorizontalDeadzone);
            delta = delta * SpeedFactor * elapsedTime;

            var (newPos, newDir) = FindTarget(elapsedTime, delta);
            float lerpSpeed = curCamDistance > MinFastLerpDistance
                ? FastLerpSpeed
                : SlowLerpSpeed;
            Lerp(elapsedTime, lerpSpeed, ref newPos, ref newDir);

            if (curCamDistance < NearHeightDistance)
                newPos.Y += (NearHeightDistance - curCamDistance) * NearHeightFactor;

            camera.Location.LocalPosition = newPos;
            camera.Location.LookIn(-newDir); // cameras point backwards
        }

        private (Vector3 pos, Vector3 dir) FindTarget(float elapsedTime, Vector2 delta)
        {
            var newCamPos = playerLocation.LocalPosition + Vector3.UnitY * AdditionalHeight;
            currentVerAngle = Math.Clamp(currentVerAngle + delta.Y, -MaxVerAngle, +MaxVerAngle);

            var newCamDir = new Vector3(MathF.Cos(currentVerAngle));
            newCamDir.Y = MathF.Sin(currentVerAngle) + playerLocation.InnerForward.Y;
            newCamDir = Vector3.Normalize(newCamDir);
            newCamPos -= newCamDir * new Vector3(1f, 0.5f, 1f) * maxCameraDistance;

            var leftClipDistance = GetClipDistance(newCamPos, -1f);
            var rightClipDistance = GetClipDistance(newCamPos, +1f);
            var clipDistance =
                leftClipDistance < rightClipDistance ? leftClipDistance
                : rightClipDistance < leftClipDistance ? rightClipDistance
                : maxCameraDistance; // no clipping geometry in sight

            if (clipDistance > curCamDistance)
            {
                curCamDistance += elapsedTime * BackwardSpeed;
                curCamDistance = Math.Min(maxCameraDistance, curCamDistance);
            }
            if (clipDistance < curCamDistance) // this is framerate dependant
                curCamDistance -= (curCamDistance - clipDistance) * ForwardFraction;

            newCamPos = playerLocation.LocalPosition + Vector3.UnitY * ClipDistance;
            newCamPos -= newCamDir * CameraDirectionFactor * curCamDistance;

            // TODO: Add water handling for the overworld camera

            return (newCamPos, newCamDir);
        }

        private float GetClipDistance(Vector3 baseCamPos, float direction)
        {
            var camRight = camera.Location.InnerRight;
            var target = baseCamPos + camRight * ClipDistance * direction;
            target.Y -= direction * ClipDistance;
            var cast = worldCollider.Cast(new Line(playerLocation.LocalPosition, target));
            return cast?.Distance ?? float.MaxValue;
        }
    }
}
