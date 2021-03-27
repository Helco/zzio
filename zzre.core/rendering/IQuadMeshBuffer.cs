using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Veldrid;

namespace zzre.rendering
{
    public interface IQuadMeshBuffer<TVertex> : IDisposable where TVertex : unmanaged
    {
        int QuadCount { get; }
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
}
