using System;
using System.Numerics;
using System.Linq;
using DefaultEcs.System;
using zzio.scn;

namespace zzre.game.systems
{
    public class TriggerCamera : BaseCamera
    {
        private const int MajorModeOriginalDir = 0;
        private const int MajorModeTriggerDir = 30;
        private const int MajorModeLookAtNpc = 50;
        private const double LerpSpeed = 9.999999960041972e-13;

        private readonly Scene scene;
        private readonly IDisposable setCameraModeDisposable;

        private Trigger trigger = new Trigger();
        private Location npcLocation = new Location();
        private int majorMode;

        public TriggerCamera(ITagContainer diContainer) : base(diContainer)
        {
            scene = diContainer.GetTag<Scene>();
            setCameraModeDisposable = world.Subscribe<messages.SetCameraMode>(HandleSetCameraMode);
        }

        public override void Dispose()
        {
            base.Dispose();
            setCameraModeDisposable.Dispose();
        }

        private void HandleSetCameraMode(in messages.SetCameraMode mode)
        {
            majorMode = mode.Mode / 100;
            if (majorMode != 0 && majorMode != 30 && majorMode != 50)
                return;

            int triggerI = mode.Mode % 100;
            var newTrigger = scene.triggers
                .Where(t => t.type == TriggerType.CameraPosition)
                .FirstOrDefault(t => t.ii1 == triggerI);
            if (newTrigger == null)
                return;

            IsEnabled = majorMode != MajorModeTriggerDir; // no update necessary for trigger dir
            trigger = newTrigger;
            npcLocation = mode.NPCEntity.Get<Location>();

            if (majorMode != MajorModeOriginalDir)
                camera.Location.LocalPosition = trigger.pos;

            if (majorMode == MajorModeTriggerDir)
                camera.Location.LookIn(-trigger.dir);
        }

        public override void Update(float state)
        {
            if (!IsEnabled)
                return;

            if (majorMode == MajorModeOriginalDir)
                camera.Location.LocalPosition = Vector3.Lerp(
                    camera.Location.LocalPosition,
                    trigger.pos,
                    (float)Math.Pow(LerpSpeed, state));

            if (majorMode == MajorModeLookAtNpc)
                camera.Location.LookNotAt(npcLocation.LocalPosition);
        }
    }
}
