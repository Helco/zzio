using ImGuiNET;
using System;
using System.Numerics;

namespace zzre.imgui;

public class FlyControlsTag
{
    private const float DefaultSpeed = 10.0f;
    private const float DragSpeedFactor = 0.025f;

    private readonly FramebufferArea fbArea;
    private readonly MouseEventArea mouseArea;
    private readonly GameTime gameTime;
    private readonly Location target;
    private float speed = DefaultSpeed;
    private Vector2 cameraAngle;

    public FlyControlsTag(Window window, Location target, ITagContainer diContainer)
    {
        window.AddTag(this);
        this.target = target;
        gameTime = diContainer.GetTag<GameTime>();
        fbArea = window.GetTag<FramebufferArea>();
        mouseArea = window.GetTag<MouseEventArea>();
        mouseArea.OnDrag += HandleDrag;
        mouseArea.OnScroll += HandleScroll;
        ResetView();
    }

    private void HandleDrag(MouseButton button, Vector2 delta)
    {
        if (button == MouseButton.Middle)
        {
            target.LocalPosition +=
                delta.Y * speed * DragSpeedFactor * target.GlobalUp -
                delta.X * speed * DragSpeedFactor * target.GlobalRight;
            fbArea.IsDirty = true;
            return;
        }

        if (button != MouseButton.Right)
            return;

        cameraAngle.Y -= delta.X * 0.01f;
        cameraAngle.X -= delta.Y * 0.01f;
        while (cameraAngle.Y > MathF.PI) cameraAngle.Y -= 2 * MathF.PI;
        while (cameraAngle.Y < -MathF.PI) cameraAngle.Y += 2 * MathF.PI;
        cameraAngle.X = Math.Clamp(cameraAngle.X, -MathF.PI / 2.0f, MathF.PI / 2.0f);
        target.LocalRotation = Quaternion.CreateFromYawPitchRoll(cameraAngle.Y, cameraAngle.X, 0.0f);

        var moveDir = Vector3.Zero;
        var speedFactor = 1.0f;
        if (ImGui.IsKeyDown(ImGuiKey.ModShift)) speedFactor *= 2.0f;
        if (ImGui.IsKeyDown(ImGuiKey.ModCtrl)) speedFactor /= 2.0f;
        if (ImGui.IsKeyDown(ImGuiKey.S)) moveDir += target.GlobalForward;
        if (ImGui.IsKeyDown(ImGuiKey.W)) moveDir -= target.GlobalForward;
        if (ImGui.IsKeyDown(ImGuiKey.D)) moveDir += target.GlobalRight;
        if (ImGui.IsKeyDown(ImGuiKey.A)) moveDir -= target.GlobalRight;
        if (ImGui.IsKeyDown(ImGuiKey.E)) moveDir += target.GlobalUp;
        if (ImGui.IsKeyDown(ImGuiKey.Q)) moveDir -= target.GlobalUp;
        target.LocalPosition += moveDir * gameTime.Delta * speed * speedFactor;

        fbArea.IsDirty = true;
    }

    private void HandleScroll(float scroll)
    {
        speed *= MathF.Pow(2.0f, scroll * 0.3f);
    }

    public void ResetView()
    {
        cameraAngle = Vector2.Zero;
        target.LocalPosition = Vector3.Zero;
        target.LocalRotation = Quaternion.Identity;
        speed = DefaultSpeed;
        fbArea.IsDirty = true;
    }
}
