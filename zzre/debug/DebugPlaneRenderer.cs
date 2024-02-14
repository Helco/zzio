using System.Collections.Generic;
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
    private static readonly IReadOnlyList<ushort> IndexPattern = new ushort[] { 0, 1, 2, 3, 2, 1 };

    private readonly DebugDynamicMesh mesh;
    public DebugMaterial Material { get; }

    public DebugPlane[] Planes
    {
        set
        {
            mesh.Clear();
            var index = mesh.RentVertices(4 * value.Length).Start.Value;
            foreach (var plane in value)
            {
                var cameraUp = Vector3.Cross(plane.normal, Vector3.UnitY).LengthSquared() < 0.01f ? Vector3.UnitZ : Vector3.UnitY;
                var rotation = Matrix4x4.CreateLookAt(Vector3.Zero, plane.normal, cameraUp);
                var right = Vector3.Transform(Vector3.UnitX, rotation) * plane.size;
                var up = Vector3.Transform(Vector3.UnitY, rotation) * plane.size;
                mesh.AttrColor.Write(index..(index + 4)).Fill(plane.color);
                mesh.AttrPos[index++] = plane.center - right - up;
                mesh.AttrPos[index++] = plane.center + right - up;
                mesh.AttrPos[index++] = plane.center - right + up;
                mesh.AttrPos[index++] = plane.center + right + up;
            }
            mesh.SetIndicesFromPattern(IndexPattern);
        }
    }

    public DebugPlaneRenderer(ITagContainer diContainer)
    {
        mesh = new(diContainer, dynamic: false);
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
        if (mesh.IndexCount == 0)
            return;
        mesh.Update(cl);
        (Material as IMaterial).Apply(cl);
        Material.ApplyAttributes(cl, mesh);
        cl.SetIndexBuffer(mesh.IndexBuffer, DynamicMesh.IndexFormat);
        cl.DrawIndexed((uint) mesh.IndexCount);
    }
}
