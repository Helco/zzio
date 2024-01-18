using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Veldrid;
using zzio;
using zzre.materials;
using zzre.rendering;

namespace zzre;

public class DebugLineRenderer : BaseDisposable, IRenderable
{
    private readonly DebugDynamicMesh mesh;

    public DebugMaterial Material { get; }

    public DebugLineRenderer(ITagContainer diContainer)
    {
        Material = new(diContainer) { Topology = DebugMaterial.TopologyMode.Lines };
        mesh = new(diContainer, dynamic: false);
    }

    protected override void DisposeManaged()
    {
        base.DisposeManaged();
        Material.Dispose();
        mesh.Dispose();
    }

    public void Clear() => mesh.Clear();

    public void Reserve(int lineCount, bool additive = true) =>
        mesh.Reserve(lineCount * 2, additive);

    public void Add(IColor color, Vector3 start, Vector3 end) => Add(color, new Line(start, end));
    public void Add(IColor color, params Line[] lines) => Add(color, lines as IEnumerable<Line>);
    public void Add(IColor color, IEnumerable<Line> lines)
    {
        foreach (var (line, index) in lines.Indexed())
        {
            mesh.Add(new(line.Start, color));
            mesh.Add(new(line.End, color));
        }
    }

    public void Render(CommandList cl)
    {
        if (mesh.VertexCount == 0)
            return;
        mesh.Update(cl);
        (Material as IMaterial).Apply(cl);
        Material.ApplyAttributes(cl, mesh);
        cl.Draw((uint)mesh.VertexCount);
    }
}
