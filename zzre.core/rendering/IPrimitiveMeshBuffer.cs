using System;
using Veldrid;

namespace zzre.rendering
{
    public interface IPrimitiveMeshBuffer<TVertex> : IDisposable where TVertex : unmanaged
    {
        int PrimitiveCount { get; }
        DeviceBuffer VertexBuffer { get; }
        DeviceBuffer IndexBuffer { get; }

        Span<TVertex> this[Index index] { get; }
        Span<TVertex> this[Range range] { get; }
        TVertex this[Index quadIndex, Index vertexIndex] { get; set; }

        Range Reserve(int quadCount);
        void Release(Range range);
        void Update(CommandList cl);
        void Render(CommandList cl, Range? quadRange = null, uint instanceStart = 1, uint instanceCount = 1);
    }

    public interface IQuadMeshBuffer<TVertex> : IPrimitiveMeshBuffer<TVertex> where TVertex : unmanaged
    {
        // invariant: VerticesPerPrimitive == 4
        // invariant: IndicesPerPrimitive == 6
    }
}
