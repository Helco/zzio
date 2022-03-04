using System;
using System.Numerics;
using System.Linq;
using DefaultEcs.System;

namespace zzre.game.systems
{
    internal class CreatureCamera : BaseCamera
    {
        private enum SubMode
        {
            LeftTop = 0,
            LeftBottom,
            LeftCenter,
            RightTop,
            RightBottom,
            RightCenter,
            Overworld, // ignored by CreatureCamera, Funatics and its special cases -_-
            Behind,
            Front
        }

        private readonly Game game;
        private readonly IDisposable setCameraModeSubscription;

        private DefaultEcs.Entity source, target;
        private Location sourceLoc = new Location();
        private Location targetLoc = new Location();
        private SubMode mode;

        public CreatureCamera(ITagContainer diContainer) : base(diContainer)
        {
            game = diContainer.GetTag<Game>();
            setCameraModeSubscription = world.Subscribe<messages.SetCameraMode>(HandleSetCameraMode);
        }

        public override void Dispose()
        {
            base.Dispose();
            setCameraModeSubscription?.Dispose();
        }

        private void HandleSetCameraMode(in messages.SetCameraMode message)
        {
            var majorMode = message.Mode / 100;
            mode = (SubMode)(message.Mode % 100);
            if ((majorMode != 10 && majorMode != 20) || mode == SubMode.Overworld)
                return;

            Location npcLocation;
            if (message.NPCEntity.IsAlive)
                npcLocation = message.NPCEntity.Get<Location>();
            else
            {
                npcLocation = new Location();
                npcLocation.LocalPosition =
                    camera.Location.GlobalPosition +
                    camera.Location.GlobalForward;
            }
            (sourceLoc, targetLoc) = majorMode == 10
                ? (playerLocation, npcLocation)
                : (npcLocation, playerLocation);
            (source, target) = majorMode == 10
                ? (game.PlayerEntity, message.NPCEntity)
                : (message.NPCEntity, game.PlayerEntity);

            IsEnabled = true;
        }

        public override void Update(float elapsedTime)
        {
            if (!IsEnabled)
                return;

            switch (mode)
            {
                case SubMode.LeftTop: TowardsLeftTop(elapsedTime); break;
                case SubMode.LeftBottom: TowardsRelative(elapsedTime, 0.4f, -0.4f, -0.2f); break;
                case SubMode.LeftCenter: TowardsRelative(elapsedTime, 0.4f, -0.4f, 0.4f); break;
                case SubMode.RightTop: TowardsRelative(elapsedTime, -0.4f, -0.4f, 0.8f); break;
                case SubMode.RightBottom: TowardsRelative(elapsedTime, -0.4f, -0.4f, -0.2f); break;
                case SubMode.RightCenter: TowardsRelative(elapsedTime, -0.4f, -0.4f, 0.4f); break;
                case SubMode.Front: Towards(elapsedTime, sourceLoc.LocalPosition); break;
                case SubMode.Behind: Behind(); break;
                default: throw new NotSupportedException($"Unsupported CreatureCamera sub-mode: {mode}");
            }
        }

        private void TowardsLeftTop(float elapsedTime)
        {
            var targetDir = source.IsAlive
                ? source.Get<components.PuppetActorMovement>().TargetDirection
                : sourceLoc.GlobalForward;
            var horFlipTargetDir = new Vector3(targetDir.Z, targetDir.Y, -targetDir.X);

            Towards(elapsedTime, sourceLoc.LocalPosition
                + horFlipTargetDir * 0.7f
                + targetDir * -0.4f
                + Vector3.UnitY * 0.8f);
        }

        private void TowardsRelative(float elapsedTime, float right, float forward, float upwards) =>
            Towards(elapsedTime, sourceLoc.LocalPosition
                + sourceLoc.GlobalRight * right
                + sourceLoc.GlobalForward * forward
                + Vector3.UnitY * upwards);

        private const float MinCamDistance = 1.1f;
        private void Towards(float elapsedTime, Vector3 targetPos)
        {
            var camToTarget = MathEx.SafeNormalize(targetLoc.LocalPosition - camera.Location.LocalPosition);
            var camDir = -camera.Location.GlobalForward;
            var newCamDir = camDir + (camToTarget - camDir) * elapsedTime;
            camera.Location.LookNotIn(newCamDir);

            var newPos = camera.Location.LocalPosition;
            newPos += (targetPos - newPos) * elapsedTime;
            if (Vector3.Distance(newPos, sourceLoc.LocalPosition) < MinCamDistance)
                newPos = sourceLoc.LocalPosition +
                    MathEx.SafeNormalize(newPos - sourceLoc.LocalPosition) * MinCamDistance;
            camera.Location.LocalPosition = newPos;
        }

        private void Behind()
        {
            camera.Location.LocalPosition = sourceLoc.LocalPosition + sourceLoc.GlobalForward * 0.56f + Vector3.UnitY;
            camera.Location.LookNotAt(targetLoc.LocalPosition);
        }
    }
}
