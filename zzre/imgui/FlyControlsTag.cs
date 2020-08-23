using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Numerics;
using Veldrid;
using zzre.rendering;

namespace zzre.imgui
{
    public class FlyControlsTag : BaseDisposable
    {
        private const float FieldOfView = 60.0f * 3.141592653f / 180.0f;
        private const float DefaultSpeed = 10.0f;

        private readonly FramebufferArea fbArea;
        private readonly MouseEventArea mouseArea;
        private readonly GameTime gameTime;
        private Vector3 position = Vector3.Zero;
        private Vector2 cameraAngle = Vector2.Zero;
        private float speed = DefaultSpeed;

        private Matrix4x4 RotationMatrix => Matrix4x4.CreateRotationY(cameraAngle.X) * Matrix4x4.CreateRotationX(cameraAngle.Y);
        private Matrix4x4 InvRotationMatrix => Matrix4x4.CreateRotationX(-cameraAngle.Y) * Matrix4x4.CreateRotationY(-cameraAngle.X);

        public UniformBuffer<Matrix4x4> Projection { get; }
        public UniformBuffer<Matrix4x4> View { get; }
        public UniformBuffer<Matrix4x4> World { get; }

        public Vector3 Position
        {
            get => position;
            set
            {
                position = value;
                UpdateCamera();
            }
        }

        public FlyControlsTag(Window window, ITagContainer diContainer)
        {
            window.AddTag(this);
            var device = diContainer.GetTag<GraphicsDevice>();
            gameTime = diContainer.GetTag<GameTime>();
            fbArea = window.GetTag<FramebufferArea>();
            fbArea.OnRender += HandleRender;
            fbArea.OnResize += HandleResize;
            mouseArea = window.GetTag<MouseEventArea>();
            mouseArea.OnDrag += HandleDrag;
            mouseArea.OnScroll += HandleScroll;

            Projection = new UniformBuffer<Matrix4x4>(device.ResourceFactory);
            View = new UniformBuffer<Matrix4x4>(device.ResourceFactory);
            World = new UniformBuffer<Matrix4x4>(device.ResourceFactory);
            World.Ref = Matrix4x4.Identity;
            ResetView();
            HandleResize();
        }

        protected override void DisposeManaged()
        {
            base.DisposeManaged();
            Projection.Dispose();
            View.Dispose();
            World.Dispose();
        }

        private void HandleRender(CommandList cl)
        {
            Projection.Update(cl);
            View.Update(cl);
            World.Update(cl);
        }

        private void HandleResize()
        {
            Projection.Ref = Matrix4x4.CreatePerspectiveFieldOfView(FieldOfView, fbArea.Ratio, 0.1f, 300.0f);
        }

        private void HandleDrag(ImGuiMouseButton button, Vector2 delta)
        {
            if (button != ImGuiMouseButton.Right)
                return;

            cameraAngle += delta * 0.01f;
            while (cameraAngle.X > MathF.PI) cameraAngle.X -= 2 * MathF.PI;
            while (cameraAngle.X < -MathF.PI) cameraAngle.X += 2 * MathF.PI;
            cameraAngle.Y = Math.Clamp(cameraAngle.Y, -MathF.PI / 2.0f, MathF.PI / 2.0f);

            var moveDelta = gameTime.Delta * speed;
            if (ImGui.IsKeyDown((int)Key.ShiftLeft)) moveDelta *= 2.0f;
            if (ImGui.IsKeyDown((int)Key.W)) position += Vector3.Transform(Vector3.UnitZ * moveDelta, InvRotationMatrix);
            if (ImGui.IsKeyDown((int)Key.S)) position += Vector3.Transform(-Vector3.UnitZ * moveDelta, InvRotationMatrix);
            if (ImGui.IsKeyDown((int)Key.D)) position += Vector3.Transform(-Vector3.UnitX * moveDelta, InvRotationMatrix);
            if (ImGui.IsKeyDown((int)Key.A)) position += Vector3.Transform(Vector3.UnitX * moveDelta, InvRotationMatrix);
            if (ImGui.IsKeyDown((int)Key.E)) position += Vector3.Transform(-Vector3.UnitY * moveDelta, InvRotationMatrix);
            if (ImGui.IsKeyDown((int)Key.Q)) position += Vector3.Transform(Vector3.UnitY * moveDelta, InvRotationMatrix);

            UpdateCamera();
        }

        private void HandleScroll(float scroll)
        {
            speed = speed * MathF.Pow(2.0f, scroll * 0.3f);
        }

        public void ResetView()
        {
            position = Vector3.Zero;
            cameraAngle = Vector2.Zero;
            speed = DefaultSpeed;
            UpdateCamera();
        }

        private void UpdateCamera()
        {
            View.Ref = Matrix4x4.CreateTranslation(position) * RotationMatrix;
            fbArea.IsDirty = true;
        }
    }
}
