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
        private readonly FramebufferArea fbArea;
        private readonly MouseEventArea mouseArea;
        private readonly LocationBuffer locationBuffer;
        private readonly Location target;
        private readonly Location rotationLoc = new Location();
        private readonly DeviceBufferRange rotationLocRange;
        private float distance = 2.0f;
        private Vector2 cameraAngle = Vector2.Zero;

        public float Distance
        {
            get => distance;
            set
            {
                distance = value;
                UpdateCamera();
            }
        }

        public Vector2 CameraAngle
        {
            get => cameraAngle;
            set
            {
                cameraAngle = value;
                UpdateCamera();
            }
        }

        public OrbitControlsTag(Window window, Location target, ITagContainer diContainer)
        {
            window.AddTag(this);
            this.target = target;
            fbArea = window.GetTag<FramebufferArea>();
            mouseArea = window.GetTag<MouseEventArea>();
            mouseArea.OnDrag += HandleDrag;
            mouseArea.OnScroll += HandleScroll;
            locationBuffer = diContainer.GetTag<LocationBuffer>();
            rotationLocRange = locationBuffer.Add(rotationLoc);
            target.Parent = rotationLoc;

            ResetView();
        }

        protected override void DisposeManaged()
        {
            base.DisposeManaged();
            locationBuffer.Remove(rotationLocRange);
        }

        private void HandleDrag(MouseButton button, Vector2 delta)
        {
            if (button != MouseButton.Right)
                return;

            cameraAngle -= delta * 0.01f;
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
            rotationLoc.LocalRotation = Quaternion.CreateFromYawPitchRoll(cameraAngle.X, cameraAngle.Y, 0.0f);
            target.LocalPosition = Vector3.UnitZ * distance;
            fbArea.IsDirty = true;
        }
    }
}
