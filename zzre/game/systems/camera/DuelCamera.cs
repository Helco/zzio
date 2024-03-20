using System;
using System.Numerics;

namespace zzre.game.systems;

public sealed partial class DuelCamera : BaseGameCamera
{
    [Configuration(Description = "How fast the camera lerps towards the target position and direction")]
    private float LerpSpeed = 0.00001f;
    [Configuration(Description = "The speed of horizontal rotation")]
    private float HorizontalSpeed = 15f;
    [Configuration(Description = "The speed of vertical rotation")]
    private float VerticalSpeed = 0.3f;
    [Configuration(Description = "Max vertical angle in radians", Min = 0, Max = Math.PI / 2)]
    private float MaxVerticalAngle = 1.31125f;
    [Configuration(Description = "Additional height of camera above player center")]
    private float AddHeight = 0.25f;
    [Configuration(Description = "Minimum camera distance for zooms (unscaled)")]
    private float MinZoomDistance = 0.05f;
    [Configuration(Description = "Maximum camera distance for zooms (unscaled)")]
    private float MaxZoomDistance = 3f;
    [Configuration(Description = "Speed of zooms")]
    private float ZoomSpeed = 2f;
    [Configuration(Description = "Minimum distance at which world intersection tests are done")]
    private float MinIntersectionTestDistance = 0.3f;
    [Configuration(Description = "Length of intersection test rays")]
    private float IntersectionRayLength = 0.4f;
    [Configuration(Description = "New camera distance in case of intersection\n(in fraction of intersection distance)")]
    private float IntersectionCameraPos = 0.8f;
    [Configuration(Description = "Minimum camera speed before idle bob speed is used")]
    private float MinimumCameraBobSpeed = 0.01f;
    [Configuration(Description = "Simulated travel for bobbing of still cameras",
        Key = "/zanzarah.net.TEST_IDLE_BOB")]
    private float SimulatedBobSpeed = 0.2f;
    [Configuration(Description = "Total distance travelled for bobbing")]
    private float TotalBobTravel = 200000f;
    [Configuration(Description = "Additional factor on distance for bobbing")]
    private float BobDistanceFactor = 1500f;
    [Configuration(Description = "Cycle speed for bobbing")]
    private float BobCycle = 0.003147f;
    [Configuration(Description = "Bobbing roll amplitude",
        Key = "/zanzarah.net.TEST_BOB_ROLL")]
    private float BobRoll = 0.1f;
    [Configuration(Description = "Bobbing pitch amplitude",
        Key = "/zanzarah.net.TEST_BOB_PITCH")]
    private float BobPitch = 0.1f;
    [Configuration(Description = "Bobbing height amplitude",
        Key = "/zanzarah.net.TEST_BOB_UP")]
    private float BobHeight = 0.05f;

    private readonly IDisposable configBinding;
    private bool isFirstFrame;
    private Vector3 oldCamPos;
    private float currentVerAngle;
    private float curCamDistance;
    private float targetCamDistance;
    private float totalCamTravel;

    public DuelCamera(ITagContainer diContainer) : base(diContainer)
    {
        world.SetMaxCapacity<components.DuelCameraMode>(1);
        configBinding = diContainer.GetConfigFor(this);
        targetCamDistance = MaxZoomDistance;
    }

    public override void Dispose()
    {
        base.Dispose();
        configBinding?.Dispose();
    }

    protected override void Update(float elapsedTime, Vector2 delta)
    {
        var playerEntity = world.Get<components.PlayerEntity>().Entity;
        var playerParticipant = playerEntity.Get<components.DuelParticipant>();
        if (!playerParticipant.ActiveFairy.TryGet<Location>(out var fairyLocation))
            return;
        var duelCameraMode = playerEntity.Get<components.DuelCameraMode>();

        delta *= new Vector2(HorizontalSpeed, VerticalSpeed) * -elapsedTime;

        fairyLocation.LocalRotation *= Quaternion.CreateFromAxisAngle(Vector3.UnitY, delta.X * MathEx.DegToRad);

        var (newPos, newDir) = FindTarget(duelCameraMode, fairyLocation, elapsedTime, delta);
        newDir = -newDir; // cameras point backwards
        Lerp(elapsedTime, LerpSpeed, ref newPos, ref newDir);

        var bobPhase = UpdateBobbing(elapsedTime);
        camera.Location.LocalPosition = newPos + Vector3.UnitY * bobPhase * BobHeight;
        camera.Location.LookIn(newDir);
        camera.Location.LocalRotation *= Quaternion.CreateFromAxisAngle(newDir, bobPhase * BobRoll * MathEx.DegToRad);
        camera.Location.LocalRotation *= Quaternion.CreateFromAxisAngle(camera.Location.InnerRight, bobPhase * BobPitch * MathEx.DegToRad);
    }

    private (Vector3 pos, Vector3 dir) FindTarget(
        components.DuelCameraMode cameraMode,
        Location fairyLocation,
        float elapsedTime,
        Vector2 delta)
    {
        currentVerAngle = Math.Clamp(currentVerAngle + delta.Y, -MaxVerticalAngle, +MaxVerticalAngle);

        var zeroDistCameraPos = fairyLocation.GlobalPosition + Vector3.UnitY * AddHeight;
        var targetPos = zeroDistCameraPos;
        var (verAngleSin, verAngleCos) = MathF.SinCos(currentVerAngle);
        var targetDir = fairyLocation.GlobalForward * new Vector3(verAngleCos, 1f, verAngleCos);
        targetDir.Y += verAngleSin; // I gave up understanding Funatics
        targetDir = Vector3.Normalize(targetDir);

        curCamDistance = targetCamDistance;
        switch(cameraMode)
        {
            case components.DuelCameraMode.ZoomIn:
                targetCamDistance = Math.Max(MinZoomDistance, targetCamDistance - elapsedTime * ZoomSpeed);
                break;
            case components.DuelCameraMode.ZoomOut:
                targetCamDistance = Math.Min(MaxZoomDistance, targetCamDistance + elapsedTime * ZoomSpeed);
                break;
        }

        if (curCamDistance > MinIntersectionTestDistance)
        {
            targetPos -= targetDir * curCamDistance;
            var leftClipDistance = GetClipDistance(zeroDistCameraPos, targetPos, -1f);
            var rightClipDistance = GetClipDistance(zeroDistCameraPos, targetPos, +1f);
            if (leftClipDistance > rightClipDistance)
                curCamDistance = rightClipDistance * IntersectionCameraPos;
            if (rightClipDistance > leftClipDistance)
                curCamDistance = leftClipDistance * IntersectionCameraPos;
        }

        targetPos = zeroDistCameraPos - targetDir * curCamDistance;
        return (targetPos, targetDir);
    }

    private float GetClipDistance(Vector3 zeroDistPos, Vector3 targetPos, float dirSign)
    {
        var dir = camera.Location.InnerRight * dirSign;
        var line = new Line(zeroDistPos, targetPos + dir * IntersectionRayLength);
        var cast = worldCollider.Cast(line);
        return cast?.Distance ?? float.MaxValue;
    }

    private float UpdateBobbing(float elapsedTime)
    {
        if (isFirstFrame)
        {
            isFirstFrame = false;
            oldCamPos = camera.Location.GlobalPosition;
            totalCamTravel = 0f;
        }
        else
        {
            var lastTravel = (oldCamPos - camera.Location.GlobalPosition).Length();
            var lastSpeed = lastTravel / elapsedTime;
            if (lastSpeed < MinimumCameraBobSpeed)
                lastTravel = SimulatedBobSpeed * elapsedTime;
            totalCamTravel += lastTravel * BobDistanceFactor;
            if (totalCamTravel > TotalBobTravel)
                totalCamTravel -= TotalBobTravel;
        }
        oldCamPos = camera.Location.GlobalPosition;
        return MathF.Sin(totalCamTravel * BobCycle);
    }
}
