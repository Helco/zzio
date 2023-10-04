using System;
using System.Numerics;
using System.Linq;
using DefaultEcs.System;
using zzio.scn;

namespace zzre.game.systems;

public class TriggerCamera : BaseCamera
{
    private const int MajorModeOriginalDir = 0;
    private const int MajorModeTriggerDir = 30;
    private const int MajorModeLookAtNpc = 50;
    private const double LerpSpeed = 9.999999960041972e-13;

    private readonly IDisposable setCameraModeDisposable;

    private Trigger trigger = new();
    private Location npcLocation = new();
    private int majorMode;

    public TriggerCamera(ITagContainer diContainer) : base(diContainer)
    {
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
        var newTrigger = world.GetEntities()
            .With((in Trigger t) => t.type == TriggerType.CameraPosition && t.ii1 == triggerI)
            .AsEnumerable()
            .FirstOrDefault();
        if (newTrigger == default)
            return;

        IsEnabled = majorMode != MajorModeTriggerDir; // no update necessary for trigger dir
        trigger = newTrigger.Get<Trigger>();
        npcLocation = mode.TargetEntity.Get<Location>();

        if (majorMode != MajorModeOriginalDir)
            camera.Location.LocalPosition = trigger.pos;

        if (majorMode == MajorModeTriggerDir)
            camera.Location.LookIn(-trigger.dir);
    }

    public override void Update(float elapsedTime)
    {
        if (!IsEnabled)
            return;

        if (majorMode == MajorModeOriginalDir)
            camera.Location.LocalPosition = Vector3.Lerp(
                camera.Location.LocalPosition,
                trigger.pos,
                (float)Math.Pow(LerpSpeed, elapsedTime));

        if (majorMode == MajorModeLookAtNpc)
            camera.Location.LookNotAt(npcLocation.LocalPosition);
    }
}
