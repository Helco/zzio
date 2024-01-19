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
    private readonly DebugDynamicMesh mesh;
    public DebugMaterial Material { get; }

    public DebugPlane[] Planes
    {
        set
        {
            mesh.Clear();
            mesh.Reserve(value.Length * 4, additive: false);
            foreach (var plane in value)
            {
                var cameraUp = Vector3.Cross(plane.normal, Vector3.UnitY).LengthSquared() < 0.01f ? Vector3.UnitZ : Vector3.UnitY;
                var rotation = Matrix4x4.CreateLookAt(Vector3.Zero, plane.normal, cameraUp);
                var right = Vector3.Transform(Vector3.UnitX, rotation) * plane.size;
                var up = Vector3.Transform(Vector3.UnitY, rotation) * plane.size;
                mesh.Add(new(plane.center - right - up, plane.color));
                mesh.Add(new(plane.center + right - up, plane.color));
                mesh.Add(new(plane.center - right + up, plane.color));
                mesh.Add(new(plane.center + right + up, plane.color));
            }
        }
    }

    public DebugPlaneRenderer(ITagContainer diContainer)
    {
        mesh = new(diContainer, dynamic: false);
        mesh.IndexPattern = new ushort[] { 0, 1, 2, 3, 2, 1 };
        Material = new DebugMaterial(diContainer) { BothSided = true };
    }

    protected override void DisposeManaged()
    {
        base.DisposeManaged();
        Material.Dispose();
        mesh.Dispose();
    }

    public void Render(CommandList cl)
    {
        if (mesh.PrimitiveCount == 0)
            return;
        mesh.Update(cl);
        (Material as IMaterial).Apply(cl);
        Material.ApplyAttributes(cl, mesh);
        cl.SetIndexBuffer(mesh.IndexBuffer, mesh.IndexFormat);
        cl.DrawIndexed((uint) mesh.IndexCount);
    }
}
