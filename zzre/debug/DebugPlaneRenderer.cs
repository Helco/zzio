using System;
using System.Linq;
using System.Numerics;
using Veldrid;
using zzio;
using zzre.materials;
using zzre.rendering;

namespace zzre.debug;

public struct DebugPlane
{
    public Vector3 center;
    public Vector3 normal;
    public float size;
    public IColor color;
}

public class DebugPlaneRenderer : BaseDisposable
{
    private readonly GraphicsDevice device;
    private DeviceBuffer? vertexBuffer;
    private bool isDirty = false;
    private DebugPlane[] planes = Array.Empty<DebugPlane>();

    public DebugLegacyMaterial Material { get; }

    public DebugPlane[] Planes
    {
        get => planes;
        set
        {
            planes = value;
            isDirty = true;
        }
    }

    public DebugPlaneRenderer(ITagContainer diContainer)
    {
        device = diContainer.GetTag<GraphicsDevice>();
        Material = new DebugLegacyMaterial(diContainer);
    }

    protected override void DisposeManaged()
    {
        base.DisposeManaged();
        Material.Dispose();
        vertexBuffer?.Dispose();
    }

    private void Regenerate(CommandList cl)
    {
        vertexBuffer?.Dispose();
        if (planes.Length == 0)
        {
            vertexBuffer = null;
            return;
        }
        var vertices = planes.SelectMany(plane =>
        {
            var cameraUp = Vector3.Cross(plane.normal, Vector3.UnitY).LengthSquared() < 0.01f ? Vector3.UnitZ : Vector3.UnitY;
            var rotation = Matrix4x4.CreateLookAt(Vector3.Zero, plane.normal, cameraUp);
            var right = Vector3.Transform(Vector3.UnitX, rotation) * plane.size;
            var up = Vector3.Transform(Vector3.UnitY, rotation) * plane.size;
            var pos = new[]
            {
                plane.center - right - up,
                plane.center + right - up,
                plane.center - right + up,
                plane.center + right + up,
            };
            return new[]
            {
                new ColoredVertex(pos[0], plane.color),
                new ColoredVertex(pos[1], plane.color),
                new ColoredVertex(pos[2], plane.color),
                new ColoredVertex(pos[3], plane.color),
                new ColoredVertex(pos[2], plane.color),
                new ColoredVertex(pos[1], plane.color),
            };
        }).ToArray();

        vertexBuffer = device.ResourceFactory.CreateBuffer(new BufferDescription(
            (uint)(vertices.Length * ColoredVertex.Stride), BufferUsage.VertexBuffer));
        vertexBuffer.Name = $"DebugPlane Vertices {GetHashCode()}";
        cl.UpdateBuffer(vertexBuffer, 0, vertices);
        isDirty = false;
    }

    public void Render(CommandList cl)
    {
        if (isDirty)
            Regenerate(cl);
        if (vertexBuffer == null)
            return;
        (Material as IMaterial).Apply(cl);
        cl.SetVertexBuffer(0, vertexBuffer);
        cl.Draw((uint)(planes.Length * 6));
    }
}
