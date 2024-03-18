using System;
using System.Numerics;

namespace zzre.game.systems;

public sealed class OverworldCamera : BaseGameCamera
{
    private static readonly Vector2 SpeedFactor = new(15f, 0.1f);
    private static readonly Vector3 CameraDirectionFactor = new(1f, 0.2f, 1f);
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

    private readonly IDisposable sceneLoadedSubscription;
    private readonly IDisposable playerEnteredSubscription;
    private readonly IDisposable setCameraModeDisposable;
    private float maxCameraDistance;
    private float currentVerAngle;
    private float curCamDistance;

    public OverworldCamera(ITagContainer diContainer) : base(diContainer)
    {
        curCamDistance = maxCameraDistance;

        sceneLoadedSubscription = world.Subscribe<messages.SceneLoaded>(HandleSceneLoaded);
        playerEnteredSubscription = world.Subscribe<messages.PlayerEntered>(HandlePlayerEntered);
        setCameraModeDisposable = world.Subscribe<messages.SetCameraMode>(HandleSetCameraMode);
    }

    public override void Dispose()
    {
        base.Dispose();
        sceneLoadedSubscription.Dispose();
        playerEnteredSubscription.Dispose();
        setCameraModeDisposable.Dispose();
    }

    private void HandleSceneLoaded(in messages.SceneLoaded message)
    {
        maxCameraDistance = message.Scene.dataset.isInterior
            ? MaxCamDistInterior
            : MaxCamDistExterior;
    }

    private void HandlePlayerEntered(in messages.PlayerEntered _)
    {
        currentVerAngle = 0f;
        curCamDistance = maxCameraDistance;

        camera.Location.LookNotIn(playerLocation.GlobalForward);
        camera.Location.LocalPosition = playerLocation.GlobalPosition +
            camera.Location.InnerForward * maxCameraDistance / 3f;
        // this mimicks the original game loading freeze good enough
    }

    private void HandleSetCameraMode(in messages.SetCameraMode mode)
    {
        if (mode.Mode == -1 || mode.Mode == 1006) // the latter one is a special case by Funatics
            IsEnabled = true;
    }

    protected override void Update(float elapsedTime, Vector2 delta)
    {
        delta.X = -DeadZone(delta.X, HorizontalDeadzone);
        delta = delta * SpeedFactor * elapsedTime;

        playerLocation.LocalRotation *= Quaternion.CreateFromAxisAngle(Vector3.UnitY, delta.X * MathEx.DegToRad);

        var (newPos, newDir) = FindTarget(elapsedTime, delta);
        float lerpSpeed = curCamDistance > MinFastLerpDistance
            ? FastLerpSpeed
            : SlowLerpSpeed;
        newDir = -newDir; // cameras point backwards
        Lerp(elapsedTime, lerpSpeed, ref newPos, ref newDir);

        if (curCamDistance < NearHeightDistance)
            newPos.Y += (NearHeightDistance - curCamDistance) * NearHeightFactor;

        camera.Location.LocalPosition = newPos;
        camera.Location.LookIn(newDir);
    }

    private (Vector3 pos, Vector3 dir) FindTarget(float elapsedTime, Vector2 delta)
    {
        var newCamPos = playerLocation.LocalPosition + Vector3.UnitY * AdditionalHeight;
        currentVerAngle = Math.Clamp(currentVerAngle + delta.Y, -MaxVerAngle, +MaxVerAngle);

        var newCamDir = new Vector3(MathF.Cos(currentVerAngle)) * playerLocation.InnerForward;
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

        newCamPos = playerLocation.LocalPosition + Vector3.UnitY * (ClipDistance + AdditionalHeight);
        newCamPos -= newCamDir * CameraDirectionFactor * curCamDistance;

        // TODO: Add water handling for the overworld camera

        return (newCamPos, newCamDir);
    }

    private float GetClipDistance(Vector3 baseCamPos, float direction)
    {
        var camRight = -camera.Location.InnerRight;
        var target = baseCamPos + camRight * ClipDistance * direction;
        target.Y -= direction * ClipDistance;
        var cast = worldCollider.Cast(new Line(playerLocation.LocalPosition, target));
        return cast?.Distance ?? float.MaxValue;
    }
}
