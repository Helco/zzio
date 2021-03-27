using System;
using System.Collections.Generic;
using System.Linq;
using Veldrid;

namespace zzre.rendering
{
    public class DynamicQuadMeshBuffer<TVertex> : BaseDisposable, IQuadMeshBuffer<TVertex> where TVertex : unmanaged
    {
        private readonly ResourceFactory resourceFactory;
        private readonly RangeCollection freeQuads;
        private readonly RangeCollection dirtyQuads;
        private readonly float growFactor;
        private TVertex[] vertices;
        private DeviceBuffer? vertexBuffer, indexBuffer;

        public int Capacity => vertices.Length / 4;
        public int QuadCount => Capacity - freeQuads.Area;

        public unsafe DeviceBuffer VertexBuffer
        {
            get
            {
                var newVertexBufferSize = (uint)(Capacity * sizeof(TVertex));
                if (vertexBuffer?.SizeInBytes >= newVertexBufferSize)
                    return vertexBuffer;
                vertexBuffer?.Dispose();
                vertexBuffer = resourceFactory.CreateBuffer(new BufferDescription(newVertexBufferSize, BufferUsage.VertexBuffer));
                return vertexBuffer;
            }
        }

        public unsafe DeviceBuffer IndexBuffer
        {
            get
            {
                var newIndexBufferSize = (uint)(Capacity * sizeof(ushort) * 6);
                if (indexBuffer?.SizeInBytes >= newIndexBufferSize)
                    return indexBuffer;
                indexBuffer?.Dispose();
                indexBuffer = resourceFactory.CreateBuffer(new BufferDescription(newIndexBufferSize, BufferUsage.IndexBuffer));
                return indexBuffer;
            }
        }

        public Span<TVertex> this[Index index] => this[index..index.Offset(1)];
        public Span<TVertex> this[Range range]
        {
            get
            {
#if DEBUG
                if (freeQuads.Intersects(range))
                    throw new ArgumentException("Range is not fully reserved");
#endif
                dirtyQuads.Add(range);
                var (offset, length) = range.GetOffsetAndLength(Capacity);
                return vertices.AsSpan(offset * 4, length * 4);
            }
        }

        public TVertex this[Index quadIndex, Index vertexIndex]
        {
            get => vertices[GetVertexOffset(quadIndex, vertexIndex)];
            set
            {
                var vertexOffset = GetVertexOffset(quadIndex, vertexIndex);
                vertices[vertexOffset] = value;
                dirtyQuads.Add((vertexOffset / 4)..(vertexOffset / 4 + 1));
            }
        }

        public DynamicQuadMeshBuffer(ResourceFactory resourceFactory, int initialCapacity = 64, float growFactor = 1.5f)
        {
            this.resourceFactory = resourceFactory;
            this.growFactor = growFactor;
            freeQuads = new RangeCollection(initialCapacity) { Range.All };
            dirtyQuads = new RangeCollection(initialCapacity);
            vertices = new TVertex[initialCapacity * 4];
        }

        protected override void DisposeManaged()
        {
            base.DisposeManaged();
            vertexBuffer?.Dispose();
            indexBuffer?.Dispose();
        }

        public Range Reserve(int quadCount)
        {
            var bestFitQuad = freeQuads
                .OrderBy(r => r.End.Value - r.Start.Value)
                .Where(r => r.End.Value - r.Start.Value >= quadCount)
                .FirstOrDefault();
            if (bestFitQuad.Equals(default))
            {
                var newCapacity = Capacity;
                var resultRange = newCapacity..(newCapacity + quadCount);
                while (newCapacity < vertices.Length + quadCount)
                    newCapacity = (int)(newCapacity * growFactor);

                Array.Resize(ref vertices, newCapacity * 4);
                freeQuads.MaxRangeValue = dirtyQuads.MaxRangeValue = newCapacity;
                return resultRange;
            }

            var range = bestFitQuad.Start..bestFitQuad.Start.Offset(quadCount);
            freeQuads.Remove(range);
            return range;
        }

        public void Release(Range range)
        {
            freeQuads.Add(range);
            dirtyQuads.Remove(range); // unused quads do not have to be updated
        }

        private static readonly ushort[] IndexPattern = new ushort[] { 0, 2, 1, 0, 3, 2 };
        public unsafe void Update(CommandList cl)
        {
            uint vertexBufferSize = (uint)(Capacity * 4 * sizeof(TVertex));
            uint indexBufferSize = (uint)(Capacity * 6 * sizeof(ushort));

            if ((vertexBuffer?.SizeInBytes ?? 0) < vertexBufferSize)
            {
                vertexBuffer?.Dispose();
                vertexBuffer = resourceFactory.CreateBuffer(new BufferDescription(vertexBufferSize, BufferUsage.VertexBuffer));
                dirtyQuads.Clear();
                dirtyQuads.Add(Range.All);
            }
            foreach (var range in dirtyQuads)
            {
                var (vertexOffset, byteLength) = range.GetOffsetAndLength(Capacity);
                vertexOffset *= 4;
                var byteOffset = vertexOffset * sizeof(TVertex);
                byteLength *= 4 * sizeof(TVertex);
                cl.UpdateBuffer(vertexBuffer, (uint)byteOffset, ref vertices[vertexOffset], (uint)byteLength);
            }

            if ((indexBuffer?.SizeInBytes ?? 0) < indexBufferSize)
            {
                indexBuffer?.Dispose();
                indexBuffer = resourceFactory.CreateBuffer(new BufferDescription(indexBufferSize, BufferUsage.IndexBuffer));
                var indices = Enumerable
                    .Range(0, Capacity)
                    .SelectMany(i => IndexPattern.Select(p => (ushort)(p + i * 4)))
                    .ToArray();
                cl.UpdateBuffer(indexBuffer, 0, indices);
            }
        }

        public void Render(CommandList cl, Range? quadRange = null, uint instanceStart = 1, uint instanceCount = 1)
        {
            Update(cl);
            var (quadOffset, quadCount) = (quadRange ?? Range.All).GetOffsetAndLength(Capacity);
            var quadsToRender = new RangeCollection();
            quadsToRender.Add(new Range(quadOffset, quadOffset + quadCount));
            foreach (var free in freeQuads)
                quadsToRender.Remove(free);

            cl.SetVertexBuffer(0, VertexBuffer);
            cl.SetIndexBuffer(IndexBuffer, IndexFormat.UInt16);
            foreach (var toRender in quadsToRender)
            {
                var (toRenderOff, toRenderCount) = toRender.GetOffsetAndLength(Capacity);
                cl.DrawIndexed(6 * (uint)toRenderCount, instanceCount, (uint)toRenderOff * 6, 0, instanceStart);
            }
        }

        private int GetVertexOffset(Index quadIndex, Index vertexIndex)
        {
            var vertexOffset = vertexIndex.GetOffset(4);
            if (vertexOffset < 0 || vertexOffset >= 4)
                throw new ArgumentOutOfRangeException(nameof(vertexIndex));
            var quadOffset = quadIndex.GetOffset(Capacity);
            if (quadOffset < 0 || quadOffset >= Capacity)
                throw new ArgumentOutOfRangeException(nameof(quadOffset));
            return quadOffset * 4 + vertexOffset;
        }
    }
}
