using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Veldrid;
using zzio;
using zzre.materials;
using zzre.rendering;

namespace zzre;

[Flags]
public enum AxisPlanes
{
    XY = (1 << 0),
    XZ = (1 << 1),
    YZ = (1 << 2)
}

public class DebugLineRenderer : BaseDisposable, IRenderable
{
    private readonly DebugDynamicMesh mesh;

    public DebugMaterial Material { get; }
    public int Count => mesh.VertexCount / 2;

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

    public void Render(CommandList cl)
    {
        if (mesh.VertexCount == 0)
            return;
        mesh.Update(cl);
        (Material as IMaterial).Apply(cl);
        Material.ApplyAttributes(cl, mesh);
        cl.Draw((uint)mesh.VertexCount);
    }

    public void Clear() => mesh.Clear();

    public void Reserve(int lineCount, bool additive = true) =>
        mesh.Reserve(lineCount * 2, additive);

    public void Add(IColor color, Vector3 start, Vector3 end) => Add(color, new Line(start, end));
    public void Add(IColor color, params Line[] lines) => Add(color, lines as IEnumerable<Line>);
    public void Add(IColor color, IEnumerable<Line> lines)
    {
        foreach (var line in lines)
        {
            mesh.Add(new(line.Start, color));
            mesh.Add(new(line.End, color));
        }
    }

    public void AddTriangles(IReadOnlyList<Triangle> triangles, IReadOnlyList<IColor> colors)
    {
        if (triangles.Count != colors.Count)
            throw new ArgumentException("Triangle and color count have to match");
        Reserve(triangles.Count * 3, additive: true);
        foreach (var (tri, col) in triangles.Zip(colors))
            Add(col, tri.Edges());
    }

    public void AddOctahedron(IReadOnlyList<Vector3> corners, IColor color)
    {
        if (corners.Count != 6)
            throw new ArgumentException("Expected 6 corners for octahedron");
        Reserve(12);
        Add(color, new Line[]
        {
            new(corners[0], corners[1]),
            new(corners[1], corners[2]),
            new(corners[2], corners[3]),
            new(corners[3], corners[0]),

            new(corners[4], corners[0]),
            new(corners[4], corners[1]),
            new(corners[4], corners[2]),
            new(corners[4], corners[3]),

            new(corners[5], corners[0]),
            new(corners[5], corners[1]),
            new(corners[5], corners[2]),
            new(corners[5], corners[3])
        });
    }

    public void AddDiamondSphere(Sphere bounds, IColor color)
    {
        var right = Vector3.UnitX * bounds.Radius;
        var up = Vector3.UnitY * bounds.Radius;
        var forward = Vector3.UnitZ * bounds.Radius;
        AddOctahedron(new[]
        {
            bounds.Center + right,
            bounds.Center + forward,
            bounds.Center - right,
            bounds.Center - forward,
            bounds.Center + up,
            bounds.Center - up,
        }, color);
    }

    public void AddHexahedron(IReadOnlyList<Vector3> corners, IColor color)
    {
        if (corners.Count != 8)
            throw new ArgumentException("Expected 8 corners for octahedron");
        Reserve(12);
        Add(color, new Line[]
        {
            new(corners[0], corners[1]),
            new(corners[0], corners[2]),
            new(corners[3], corners[1]),
            new(corners[3], corners[2]),

            new(corners[4], corners[5]),
            new(corners[4], corners[6]),
            new(corners[7], corners[5]),
            new(corners[7], corners[6]),

            new(corners[0], corners[4]),
            new(corners[1], corners[5]),
            new(corners[2], corners[6]),
            new(corners[3], corners[7]),
        });
    }

    public void AddBox(OrientedBox box, IColor color) =>
        AddHexahedron(box.Corners(), color);

    public void AddGrid(
        IColor gridColor,
        int cellCountX,
        int cellCountY,
        int cellCountZ,
        Vector3 cellSize,
        AxisPlanes planes = AxisPlanes.XZ,
        Vector3? origin = null,
        Vector3? originSize = null)
    {
        var o = origin.GetValueOrDefault(Vector3.Zero);

        IEnumerable<Line> GenerateParallelLines(Vector3 dir, Vector3 reach, int count)
        {
            float distance = Vector3.Dot(cellSize, dir);
            return Enumerable
                .Range(-count, count * 2 + 1)
                .Select(i => o + dir * distance * i) // center of line
                .Select(center => new Line(center - reach, center + reach));
        }

        static int PlaneLineCount(int count1, int count2) => (count1 + count2 + 1) * 2;

        var lines = Enumerable.Empty<Line>();
        int lineCount = originSize is null ? 0 : 3;
        Vector3 xReach = Vector3.UnitX * cellCountX * cellSize.X;
        Vector3 yReach = Vector3.UnitY * cellCountY * cellSize.Y;
        Vector3 zReach = Vector3.UnitZ * cellCountZ * cellSize.Z;
        if (planes.HasFlag(AxisPlanes.XY))
        {
            lineCount += PlaneLineCount(cellCountX, cellCountY);
            lines = lines
                .Concat(GenerateParallelLines(Vector3.UnitX, yReach, cellCountX))
                .Concat(GenerateParallelLines(Vector3.UnitY, xReach, cellCountY));
        }
        if (planes.HasFlag(AxisPlanes.XZ))
        {
            lineCount += PlaneLineCount(cellCountX, cellCountZ);
            lines = lines
                .Concat(GenerateParallelLines(Vector3.UnitX, zReach, cellCountX))
                .Concat(GenerateParallelLines(Vector3.UnitZ, xReach, cellCountZ));
        }
        if (planes.HasFlag(AxisPlanes.YZ))
        {
            lineCount += PlaneLineCount(cellCountY, cellCountZ);
            lines = lines
                .Concat(GenerateParallelLines(Vector3.UnitY, zReach, cellCountY))
                .Concat(GenerateParallelLines(Vector3.UnitZ, yReach, cellCountZ));
        }

        Reserve(lineCount, additive: true);
        Add(gridColor, lines);
        if (originSize is not null)
        {
            Add(IColor.Red, new Line(o, o + Vector3.UnitX * originSize.Value.X));
            Add(IColor.Green, new Line(o, o + Vector3.UnitY * originSize.Value.Y));
            Add(IColor.Blue, new Line(o, o + Vector3.UnitZ * originSize.Value.Z));
        }
    }

    public void AddGrid(
        IColor gridColor,
        int cellCount,
        Vector3 cellSize,
        AxisPlanes planes = AxisPlanes.XZ,
        Vector3? origin = null,
        Vector3? originSize = null) =>
        AddGrid(gridColor, cellCount, cellCount, cellCount, cellSize, planes, origin, originSize);

    public void AddGrid() => AddGrid(
        cellSize: Vector3.One * 1f,
        cellCount: 5,
        originSize: Vector3.One * 5f,
        gridColor: new IColor(0xFF888888));
}
