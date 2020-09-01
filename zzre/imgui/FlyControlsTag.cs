using ImGuiNET;
using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.Generic;
using System.Numerics;
using Veldrid;
using zzre.rendering;

namespace zzre.imgui
{
    public class FlyControlsTag
    {
        private const float DefaultSpeed = 10.0f;
        private const float DragSpeedFactor = 0.025f;

        private readonly FramebufferArea fbArea;
        private readonly MouseEventArea mouseArea;
        private readonly GameTime gameTime;
        private readonly Location location;
        private float speed = DefaultSpeed;
        private Vector2 cameraAngle;

        public FlyControlsTag(Window window, Location location, ITagContainer diContainer)
        {
            window.AddTag(this);
            this.location = location;
            var device = diContainer.GetTag<GraphicsDevice>();
            gameTime = diContainer.GetTag<GameTime>();
            fbArea = window.GetTag<FramebufferArea>();
            mouseArea = window.GetTag<MouseEventArea>();
            mouseArea.OnDrag += HandleDrag;
            mouseArea.OnScroll += HandleScroll;
            ResetView();
        }

        private void HandleDrag(ImGuiMouseButton button, Vector2 delta)
        {
            if (button == ImGuiMouseButton.Middle)
            {
                location.LocalPosition +=
                    delta.Y * speed * DragSpeedFactor * location.GlobalUp -
                    delta.X * speed * DragSpeedFactor * location.GlobalRight;
                fbArea.IsDirty = true;
                return;
            }

            if (button != ImGuiMouseButton.Right)
                return;

            cameraAngle.Y -= delta.X * 0.01f;
            cameraAngle.X -= delta.Y * 0.01f;
            while (cameraAngle.Y > MathF.PI) cameraAngle.Y -= 2 * MathF.PI;
            while (cameraAngle.Y < -MathF.PI) cameraAngle.Y += 2 * MathF.PI;
            cameraAngle.X = Math.Clamp(cameraAngle.X, -MathF.PI / 2.0f, MathF.PI / 2.0f);
            location.LocalRotation = Quaternion.CreateFromYawPitchRoll(cameraAngle.Y, cameraAngle.X, 0.0f);

            var moveDir = Vector3.Zero;
            var speedFactor = 1.0f;
            if (ImGui.IsKeyDown((int)Key.ShiftLeft)) speedFactor *= 2.0f;
            if (ImGui.IsKeyDown((int)Key.ControlLeft)) speedFactor /= 2.0f;
            if (ImGui.IsKeyDown((int)Key.W)) moveDir += location.GlobalForward;
            if (ImGui.IsKeyDown((int)Key.S)) moveDir -= location.GlobalForward;
            if (ImGui.IsKeyDown((int)Key.D)) moveDir += location.GlobalRight;
            if (ImGui.IsKeyDown((int)Key.A)) moveDir -= location.GlobalRight;
            if (ImGui.IsKeyDown((int)Key.E)) moveDir += location.GlobalUp;
            if (ImGui.IsKeyDown((int)Key.Q)) moveDir -= location.GlobalUp;
            location.LocalPosition = location.LocalPosition + moveDir * gameTime.Delta * speed * speedFactor;

            fbArea.IsDirty = true;
        }

        private void HandleScroll(float scroll)
        {
            speed = speed * MathF.Pow(2.0f, scroll * 0.3f);
        }

        public void ResetView()
        {
            cameraAngle = Vector2.Zero;
            location.LocalPosition = Vector3.Zero;
            location.LocalRotation = Quaternion.Identity;
            speed = DefaultSpeed;
            fbArea.IsDirty = true;
        }
    }
}
