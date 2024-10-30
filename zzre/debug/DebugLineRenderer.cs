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

public class DebugLineRenderer : BaseDisposable
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

    public void Render(CommandList cl) => Render(cl, ..);

    public void Render(CommandList cl, Range range)
    {
        var (offset, count) = range.GetOffsetAndLength(Count);
        if (count == 0)
            return;
        mesh.Update(cl);
        (Material as IMaterial).Apply(cl);
        Material.ApplyAttributes(cl, mesh);
        cl.Draw((uint)count * 2, 1, (uint)offset * 2, 0);
    }

    public void Clear() => mesh.Clear();

    public void Reserve(int additional)
    {
        var vertices = mesh.RentVertices(additional * 2);
        mesh.AttrPos.Read(vertices);
        mesh.AttrColor.Read(vertices);
        mesh.ReturnVertices(vertices);
    }

    public void Add(IColor color, Vector3 start, Vector3 end) => Add(color, new Line(start, end));
    public void Add(IColor color, params Line[] lines) => Add(color, lines as IEnumerable<Line>);
    public void Add(IColor color, IEnumerable<Line> lines)
    {
        var range = mesh.RentVertices(lines.Count() * 2);
        mesh.AttrColor.Write(range).Fill(color);
        var index = range.Start.Value;
        foreach (var line in lines)
        {
            mesh.AttrPos[index++] = line.Start;
            mesh.AttrPos[index++] = line.End;
        }
    }

    public void AddCross(IColor color, Vector3 center, float radius)
    {
        var range = mesh.RentVertices(6);
        mesh.AttrColor.Write(range).Fill(color);
        var index = range.Start.Value;
        mesh.AttrPos[index++] = center - Vector3.UnitX * radius;
        mesh.AttrPos[index++] = center + Vector3.UnitX * radius;
        mesh.AttrPos[index++] = center - Vector3.UnitY * radius;
        mesh.AttrPos[index++] = center + Vector3.UnitY * radius;
        mesh.AttrPos[index++] = center - Vector3.UnitZ * radius;
        mesh.AttrPos[index++] = center + Vector3.UnitZ * radius;
    }

    public void AddTriangles(IColor color, IEnumerable<Triangle> triangles)
    {
        Add(color, triangles.SelectMany(t => t.Edges()));
    }

    public void AddOctahedron(IReadOnlyList<Vector3> corners, IColor color)
    {
        if (corners.Count != 6)
            throw new ArgumentException("Expected 6 corners for octahedron");
        Add(color,
        [
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
        ]);
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
        Add(color,
        [
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
        ]);
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

        var lines = Enumerable.Empty<Line>();
        int lineCount = originSize is null ? 0 : 3;
        Vector3 xReach = Vector3.UnitX * cellCountX * cellSize.X;
        Vector3 yReach = Vector3.UnitY * cellCountY * cellSize.Y;
        Vector3 zReach = Vector3.UnitZ * cellCountZ * cellSize.Z;
        if (planes.HasFlag(AxisPlanes.XY))
        {
            lines = lines
                .Concat(GenerateParallelLines(Vector3.UnitX, yReach, cellCountX))
                .Concat(GenerateParallelLines(Vector3.UnitY, xReach, cellCountY));
        }
        if (planes.HasFlag(AxisPlanes.XZ))
        {
            lines = lines
                .Concat(GenerateParallelLines(Vector3.UnitX, zReach, cellCountX))
                .Concat(GenerateParallelLines(Vector3.UnitZ, xReach, cellCountZ));
        }
        if (planes.HasFlag(AxisPlanes.YZ))
        {
            lines = lines
                .Concat(GenerateParallelLines(Vector3.UnitY, zReach, cellCountY))
                .Concat(GenerateParallelLines(Vector3.UnitZ, yReach, cellCountZ));
        }

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
