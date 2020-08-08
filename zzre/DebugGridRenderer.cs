using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using Veldrid;
using zzio.primitives;
using zzre.materials;
using zzre.rendering;

namespace zzre
{
    [Flags]
    public enum AxisPlanes
    {
        XY = (1 << 0),
        XZ = (1 << 1),
        YZ = (1 << 2)
    }

    public class DebugGridRenderer : BaseDisposable
    {
        private readonly GraphicsDevice device;
        private DeviceBuffer vertexBuffer;

        public DebugLinesMaterial Material { get; }

        public DebugGridRenderer(ITagContainer diContainer)
        {
            Material = new DebugLinesMaterial(diContainer);
            device = diContainer.GetTag<GraphicsDevice>();
            
            vertexBuffer = null!;
            GenerateGrid(cellCount: 5, originSize: 5f, gridColor: new IColor(0xFF888888), originColor: new IColor(0xFF222222));
        }

        public void GenerateGrid(
            IColor gridColor,
            int cellCount,
            float cellSize = 1.0f,
            Vector3? origin = null,
            IColor? originColor = null,
            float originSize = float.NaN) =>
            GenerateGrid(gridColor, cellCount, cellCount, cellCount, Vector3.One * cellSize,
                originColor: originColor, origin: origin,
                originSize: float.IsNaN(originSize) ? null : new Vector3?(Vector3.One * originSize));

        public void GenerateGrid(
            IColor gridColor,
            int cellCountX,
            int cellCountY,
            int cellCountZ,
            Vector3 cellSize,
            AxisPlanes planes = AxisPlanes.XZ,
            IColor? originColor = null,
            Vector3? origin = null,
            Vector3? originSize = null)
        {
            var o = origin.GetValueOrDefault(Vector3.Zero);
            var oColor = originColor.GetValueOrDefault(gridColor);

            IEnumerable<ColoredVertex> GenerateFor(Vector3 dir, Vector3 reach, int count, float cellSize) => Enumerable
                .Range(-count, count * 2 + 1)
                .SelectMany(dist => new[]
                {
                    o + dir * dist * cellSize - reach,
                    o + dir * dist * cellSize + reach
                })
                .Select(p => new ColoredVertex(p, gridColor));

            Vector3 xReach = Vector3.UnitX * cellCountX * cellSize.X;
            Vector3 yReach = Vector3.UnitY * cellCountY * cellSize.Y;
            Vector3 zReach = Vector3.UnitZ * cellCountZ * cellSize.Z;
            var newVertices = Enumerable.Empty<ColoredVertex>();
            if (planes.HasFlag(AxisPlanes.XY))
            {
                newVertices = newVertices
                    .Concat(GenerateFor(Vector3.UnitX, yReach, cellCountX, cellSize.X))
                    .Concat(GenerateFor(Vector3.UnitY, xReach, cellCountY, cellSize.Y));
            }
            if (planes.HasFlag(AxisPlanes.XZ))
            {
                newVertices = newVertices
                    .Concat(GenerateFor(Vector3.UnitX, zReach, cellCountX, cellSize.X))
                    .Concat(GenerateFor(Vector3.UnitZ, xReach, cellCountZ, cellSize.Z));
            }
            if (planes.HasFlag(AxisPlanes.YZ))
            {
                newVertices = newVertices
                    .Concat(GenerateFor(Vector3.UnitY, zReach, cellCountY, cellSize.Y))
                    .Concat(GenerateFor(Vector3.UnitZ, yReach, cellCountZ, cellSize.Z));
            }    
            if (originSize.HasValue)
                newVertices = newVertices.Concat(new[]
                {
                    o, o + Vector3.UnitX * originSize.Value.X,
                    o, o + Vector3.UnitY * originSize.Value.Y,
                    o, o + Vector3.UnitZ * originSize.Value.Z
                }.Select(p => new ColoredVertex(p, oColor)));
            var vertices = newVertices.ToArray();

            vertexBuffer?.Dispose();
            vertexBuffer = device.ResourceFactory.CreateBuffer(new BufferDescription(
                (uint)vertices.Length * ColoredVertex.Stride, BufferUsage.VertexBuffer));
            device.UpdateBuffer(vertexBuffer, 0, vertices);
        }

        public void Render(CommandList cl)
        {
            (Material as IMaterial).Apply(cl);
            cl.SetVertexBuffer(0, vertexBuffer);
            cl.Draw(vertexBuffer.SizeInBytes / ColoredVertex.Stride);
        }
    }
}
