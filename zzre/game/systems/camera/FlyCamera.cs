using System;
using System.Numerics;
using KeyCode = Silk.NET.SDL.KeyCode;

namespace zzre.game.systems;

public class FlyCamera : BaseCamera
{
    private const float DefaultSpeed = 10.0f;

    private readonly float speed = DefaultSpeed;
    private Vector2 cameraAngle;

    public FlyCamera(ITagContainer diContainer) : base(diContainer)
    {
        zzContainer.OnMouseMove += HandleMouseMove;
    }

    public override void Dispose()
    {
        base.Dispose();
        zzContainer.OnMouseMove -= HandleMouseMove;
    }

    private void HandleMouseMove(Vector2 delta)
    {
        if (!IsEnabled)
            return;
        cameraAngle.Y -= delta.X * 0.01f;
        cameraAngle.X -= delta.Y * 0.01f;
        while (cameraAngle.Y > MathF.PI) cameraAngle.Y -= 2 * MathF.PI;
        while (cameraAngle.Y < -MathF.PI) cameraAngle.Y += 2 * MathF.PI;
        cameraAngle.X = Math.Clamp(cameraAngle.X, -MathF.PI / 2.0f, MathF.PI / 2.0f);
        camera.Location.LocalRotation = Quaternion.CreateFromYawPitchRoll(cameraAngle.Y, cameraAngle.X, 0.0f);
    }

    public override void Update(float elapsedTime)
    {
        if (!IsEnabled)
            return;
        var target = camera.Location;
        var moveDir = Vector3.Zero;
        var speedFactor = 1.0f;
        if (zzContainer.IsKeyDown(KeyCode.KLshift)) speedFactor *= 2.0f;
        if (zzContainer.IsKeyDown(KeyCode.KLctrl)) speedFactor /= 2.0f;
        if (zzContainer.IsKeyDown(KeyCode.KS)) moveDir += target.GlobalForward;
        if (zzContainer.IsKeyDown(KeyCode.KW)) moveDir -= target.GlobalForward;
        if (zzContainer.IsKeyDown(KeyCode.KD)) moveDir += target.GlobalRight;
        if (zzContainer.IsKeyDown(KeyCode.KA)) moveDir -= target.GlobalRight;
        if (zzContainer.IsKeyDown(KeyCode.KE)) moveDir += target.GlobalUp;
        if (zzContainer.IsKeyDown(KeyCode.KQ)) moveDir -= target.GlobalUp;
        target.LocalPosition += moveDir * elapsedTime * speed * speedFactor;
    }
}
