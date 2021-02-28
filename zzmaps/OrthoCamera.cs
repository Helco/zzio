using System;
using System.Collections.Generic;
using System.Numerics;
using Veldrid;
using zzre;
using zzre.rendering;

namespace zzmaps
{
    class OrthoCamera : BaseDisposable
    {
        private readonly LocationBuffer locationBuffer;

        private UniformBuffer<Matrix4x4> projection;
        private ResettableLazyValue<Matrix4x4> invProjection;
        private float nearPlane = 0.1f, addFarPlane = 2f;
        private Box bounds;

        public Location Location { get; } = new Location();
        public DeviceBufferRange ViewRange { get; }
        public DeviceBufferRange ProjectionRange => new DeviceBufferRange(projection.Buffer, 0, projection.Buffer.SizeInBytes);
        public Matrix4x4 View => Location.WorldToLocal;
        public Matrix4x4 Projection => projection.Value;
        public Matrix4x4 InvProjection => invProjection.Value;

        public float NearPlane
        {
            get => nearPlane;
            set
            {
                nearPlane = value;
                UpdateProjection();
            }
        }

        public float AdditionalFarPlane
        {
            get => addFarPlane;
            set
            {
                addFarPlane = value;
                UpdateProjection();
            }
        }

        public Box Bounds
        {
            get => bounds;
            set
            {
                bounds = value;
                UpdateProjection();
            }
        }

        public OrthoCamera(ITagContainer diContainer)
        {
            locationBuffer = diContainer.GetTag<LocationBuffer>();
            ViewRange = locationBuffer.Add(Location, inverted: true);
            projection = new UniformBuffer<Matrix4x4>(diContainer.GetTag<GraphicsDevice>().ResourceFactory);
            invProjection = new ResettableLazyValue<Matrix4x4>(CreateInvProjection);
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
            projection.Ref = Matrix4x4.CreateOrthographic(
                Bounds.Size.X, Bounds.Size.Z, nearPlane, Bounds.Size.Y + addFarPlane);
            Location.LocalPosition = new Vector3(
                Bounds.Center.X,
                Bounds.Max.Y - nearPlane,
                Bounds.Center.Z);
            Location.LocalRotation = Quaternion.CreateFromYawPitchRoll(0.0f, 90f * MathF.PI / 180f, 0.0f);
        }

        private Matrix4x4 CreateInvProjection()
        {
            if (!Matrix4x4.Invert(Projection, out var invProjection))
                return default;
            return invProjection;
        }
    }
}
