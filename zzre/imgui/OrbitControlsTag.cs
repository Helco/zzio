using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Numerics;
using Veldrid;
using zzre.rendering;

namespace zzre.imgui
{
    public class OrbitControlsTag : BaseDisposable
    {
        private const float FieldOfView = 60.0f * 3.141592653f / 180.0f;

        private readonly FramebufferArea fbArea;
        private readonly MouseEventArea mouseArea;
        private float distance = 2.0f;
        private Vector2 cameraAngle = Vector2.Zero;

        public UniformBuffer<Matrix4x4> Projection { get; }
        public UniformBuffer<Matrix4x4> View { get; }
        public UniformBuffer<Matrix4x4> World { get; }

        public OrbitControlsTag(Window window, ITagContainer diContainer)
        {
            var device = diContainer.GetTag<GraphicsDevice>();
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
            Projection.Ref = Matrix4x4.CreatePerspectiveFieldOfView(FieldOfView, fbArea.Ratio, 0.01f, 1000.0f);
        }

        private void HandleDrag(ImGuiMouseButton button, Vector2 delta)
        {
            if (button != ImGuiMouseButton.Right)
                return;

            cameraAngle += delta * 0.01f;
            while (cameraAngle.X > MathF.PI) cameraAngle.X -= 2 * MathF.PI;
            while (cameraAngle.X < -MathF.PI) cameraAngle.X += 2 * MathF.PI;
            cameraAngle.Y = Math.Clamp(cameraAngle.Y, -MathF.PI / 2.0f, MathF.PI / 2.0f);
            UpdateCamera();
        }

        private void HandleScroll(float scroll)
        {
            distance = distance * MathF.Pow(2.0f, -scroll * 0.1f);
            UpdateCamera();
        }

        public void ResetView()
        {
            distance = 2.0f;
            cameraAngle = Vector2.Zero;
            UpdateCamera();
        }

        private void UpdateCamera()
        {
            View.Ref = Matrix4x4.CreateRotationY(cameraAngle.X) * Matrix4x4.CreateRotationX(cameraAngle.Y) * Matrix4x4.CreateTranslation(0.0f, 0.0f, -distance);
            fbArea.IsDirty = true;
        }
    }
}
