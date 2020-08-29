using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using Veldrid;
using zzre.rendering;

namespace zzre.core.rendering
{
    public class Camera : BaseDisposable
    {
        private readonly LocationBuffer locationBuffer;

        private UniformBuffer<Matrix4x4> projection;
        private float aspect = 1.0f;
        private float vfov = 60.0f * 3.141592653f / 180.0f;
        private float nearPlane = 0.1f;
        private float farPlane = 500.0f;

        public Location Location { get; } = new Location();
        public DeviceBufferRange ViewRange { get; }
        public DeviceBufferRange ProjectionRange => new DeviceBufferRange(projection.Buffer, 0, projection.Buffer.SizeInBytes);
        public Matrix4x4 View => Location.WorldToLocal;
        public Matrix4x4 Projection => projection.Value;

        public float Aspect
        {
            get => aspect;
            set
            {
                aspect = value;
                UpdateProjection();
            }
        }

        public float VFoV
        {
            get => vfov;
            set
            {
                vfov = value;
                UpdateProjection();
            }
        }

        public float HFoV
        {
            get => aspect * vfov;
            set
            {
                vfov = value / aspect;
                UpdateProjection();
            }
        }

        public float NearPlane
        {
            get => nearPlane;
            set
            {
                nearPlane = value;
                UpdateProjection();
            }
        }

        public float FarPlane
        {
            get => farPlane;
            set
            {
                farPlane = value;
                UpdateProjection();
            }
        }

        public Camera(ITagContainer diContainer)
        {
            locationBuffer = diContainer.GetTag<LocationBuffer>();
            ViewRange = locationBuffer.Add(Location, inverted: true);
            projection = new UniformBuffer<Matrix4x4>(diContainer.GetTag<GraphicsDevice>().ResourceFactory);
            UpdateProjection();
        }

        protected override void DisposeManaged()
        {
            base.DisposeManaged();
            projection.Dispose();
            locationBuffer.Remove(ViewRange);
        }

        public void Update(CommandList cl) => projection.Update(cl);

        private void UpdateProjection()
        {
            projection.Ref = Matrix4x4.CreatePerspectiveFieldOfView(vfov, aspect, nearPlane, farPlane);
        }
    }
}
