using System;
using System.Numerics;
using Veldrid;
using zzio;

namespace zzre.rendering;

public class Camera : BaseDisposable
{
    private struct CameraView
    {
        public Matrix4x4 locationMatrix;
        public Vector4 position, direction;
    }

    private readonly UniformBuffer<Matrix4x4> projection;
    private readonly ResettableLazyValue<Matrix4x4> invProjection;
    private readonly UniformBuffer<CameraView> view;
    private float aspect = 1.0f;
    private float vfov = 60.0f * MathF.PI / 180.0f;
    private float nearPlane = 0.1f;
    private float farPlane = 500.0f;

    public Location Location { get; } = new Location();
    public DeviceBufferRange ViewRange => new(view.Buffer, 0, view.Buffer.SizeInBytes);
    public DeviceBufferRange ProjectionRange => new(projection.Buffer, 0, projection.Buffer.SizeInBytes);
    public Matrix4x4 View => Location.WorldToLocal;
    public Matrix4x4 Projection => projection.Value;
    public Matrix4x4 InvProjection => invProjection.Value;

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
        var resourceFactory = diContainer.GetTag<ResourceFactory>();
        projection = new(resourceFactory);
        view = new(resourceFactory, dynamic: true);
        invProjection = new ResettableLazyValue<Matrix4x4>(CreateInvProjection);
        UpdateProjection();
    }

    protected override void DisposeManaged()
    {
        base.DisposeManaged();
        projection.Dispose();
        view.Dispose();
    }

    public void Update(CommandList cl)
    {
        view.Ref.locationMatrix = View;
        view.Ref.position = new Vector4(Location.GlobalPosition, 1f);
        view.Ref.direction = new Vector4(-Location.InnerForward, 0f);
        view.Update(cl);
        projection.Update(cl);
    }

    private void UpdateProjection()
    {
        projection.Ref = Matrix4x4.CreatePerspectiveFieldOfView(vfov, aspect, nearPlane, farPlane);
        invProjection.Reset();
    }

    private Matrix4x4 CreateInvProjection()
    {
        if (!Matrix4x4.Invert(Projection, out var invProjection))
            return default;
        return invProjection;
    }

    public Ray RayAt(Vector2 screenPos)
    {
        var projected = Vector3.Transform(new Vector3(screenPos, 1f), InvProjection);
        var transformed = Vector3.Transform(projected, Location.LocalToWorld);
        return new Ray(
            Location.GlobalPosition,
            Vector3.Normalize(transformed - Location.GlobalPosition));
    }
}
