using System;
using System.Linq;
using Veldrid;
using zzio;

namespace zzre.rendering
{
    public class DynamicPrimitiveMeshBuffer<TVertex> : BaseDisposable, IPrimitiveMeshBuffer<TVertex> where TVertex : unmanaged
    {
        private readonly ResourceFactory resourceFactory;
        private readonly RangeCollection freePrims;
        private readonly RangeCollection dirtyPrims;
        private readonly float growFactor;
        private readonly ushort[] indexPattern;
        private readonly int verticesPerPrimitive;
        private TVertex[] vertices;
        private DeviceBuffer? vertexBuffer, indexBuffer;

        public int Capacity => vertices.Length / verticesPerPrimitive;
        public int PrimitiveCount => Capacity - freePrims.Area;
        public int VerticesPerPrimitive => verticesPerPrimitive;
        public int IndicesPerPrimitive => indexPattern.Length;

        public unsafe DeviceBuffer VertexBuffer
        {
            get
            {
                var newVertexBufferSize = (uint)(vertices.Length * sizeof(TVertex));
                if (vertexBuffer?.SizeInBytes >= newVertexBufferSize)
                    return vertexBuffer;
                vertexBuffer?.Dispose();
                vertexBuffer = resourceFactory.CreateBuffer(new BufferDescription(newVertexBufferSize, BufferUsage.VertexBuffer));
                vertexBuffer.Name = $"{GetType().Name} Vertices {GetHashCode()}";
                return vertexBuffer;
            }
        }

        public unsafe DeviceBuffer IndexBuffer
        {
            get
            {
                var newIndexBufferSize = (uint)(Capacity * sizeof(ushort) * indexPattern.Length);
                if (indexBuffer?.SizeInBytes >= newIndexBufferSize)
                    return indexBuffer;
                indexBuffer?.Dispose();
                indexBuffer = resourceFactory.CreateBuffer(new BufferDescription(newIndexBufferSize, BufferUsage.IndexBuffer));
                indexBuffer.Name = $"{GetType().Name} Indices {GetHashCode()}";
                return indexBuffer;
            }
        }

        public Span<TVertex> this[Index index] => this[index..index.Offset(1)];
        public Span<TVertex> this[Range range]
        {
            get
            {
#if DEBUG
                if (freePrims.Intersects(range))
                    throw new ArgumentException("Range is not fully reserved");
#endif
                dirtyPrims.Add(range);
                var (offset, length) = range.GetOffsetAndLength(Capacity);
                return vertices.AsSpan(offset * verticesPerPrimitive, length * verticesPerPrimitive);
            }
        }

        public TVertex this[Index quadIndex, Index vertexIndex]
        {
            get => vertices[GetVertexOffset(quadIndex, vertexIndex)];
            set
            {
                var vertexOffset = GetVertexOffset(quadIndex, vertexIndex);
                var primitiveIndex = vertexOffset / verticesPerPrimitive;
                vertices[vertexOffset] = value;
                dirtyPrims.Add(primitiveIndex..(primitiveIndex + 1));
            }
        }

        public DynamicPrimitiveMeshBuffer(ResourceFactory resourceFactory, ushort[] indexPattern, int initialCapacity = 64, float growFactor = 1.5f)
        {
            if (indexPattern.Length < 1)
                throw new ArgumentException("Index pattern needs to have at least one entry");
            this.resourceFactory = resourceFactory;
            this.growFactor = growFactor;
            this.indexPattern = indexPattern;
            freePrims = new RangeCollection(initialCapacity) { Range.All };
            dirtyPrims = new RangeCollection(initialCapacity);
            verticesPerPrimitive = indexPattern.Max() + 1;
            vertices = new TVertex[initialCapacity * verticesPerPrimitive];
        }

        protected override void DisposeManaged()
        {
            base.DisposeManaged();
            vertexBuffer?.Dispose();
            indexBuffer?.Dispose();
        }

        public Range Reserve(int primitiveCount)
        {
            var bestFitPrim = freePrims
                .OrderBy(r => r.End.Value - r.Start.Value)
                .Where(r => r.End.Value - r.Start.Value >= primitiveCount)
                .FirstOrDefault();
            if (bestFitPrim.Equals(default))
            {
                var newMinCapacity = vertices.Length / verticesPerPrimitive + primitiveCount;
                var newCapacity = Capacity;
                while (newCapacity < newMinCapacity)
                    newCapacity = (int)(newCapacity * growFactor);

                var resultRange = Capacity..(Capacity + primitiveCount);
                Array.Resize(ref vertices, newCapacity * verticesPerPrimitive);
                freePrims.MaxRangeValue = dirtyPrims.MaxRangeValue = newCapacity;
                return resultRange;
            }

            var range = bestFitPrim.Start..bestFitPrim.Start.Offset(primitiveCount);
            freePrims.Remove(range);
            return range;
        }

        public void Release(Range range)
        {
            freePrims.Add(range);
            dirtyPrims.Remove(range); // unused primitives do not have to be updated
        }

        public unsafe void Update(CommandList cl)
        {
            uint vertexBufferSize = (uint)(Capacity * verticesPerPrimitive * sizeof(TVertex));
            uint indexBufferSize = (uint)(Capacity * indexPattern.Length * sizeof(ushort));

            if ((vertexBuffer?.SizeInBytes ?? 0) < vertexBufferSize)
            {
                vertexBuffer?.Dispose();
                vertexBuffer = resourceFactory.CreateBuffer(new BufferDescription(vertexBufferSize, BufferUsage.VertexBuffer));
                vertexBuffer.Name = $"{GetType().Name} Vertices {GetHashCode()}";
                dirtyPrims.Clear();
                dirtyPrims.Add(Range.All);
            }
            foreach (var range in dirtyPrims)
            {
                var (vertexOffset, byteLength) = range.GetOffsetAndLength(Capacity);
                vertexOffset *= verticesPerPrimitive;
                var byteOffset = vertexOffset * sizeof(TVertex);
                byteLength *= verticesPerPrimitive * sizeof(TVertex);
                cl.UpdateBuffer(vertexBuffer, (uint)byteOffset, ref vertices[vertexOffset], (uint)byteLength);
            }

            if ((indexBuffer?.SizeInBytes ?? 0) < indexBufferSize)
            {
                indexBuffer?.Dispose();
                indexBuffer = resourceFactory.CreateBuffer(new BufferDescription(indexBufferSize, BufferUsage.IndexBuffer));
                indexBuffer.Name = $"{GetType().Name} Indices {GetHashCode()}";
                var indices = Enumerable
                    .Range(0, Capacity)
                    .SelectMany(i => indexPattern.Select(p => (ushort)(p + i * verticesPerPrimitive)))
                    .ToArray();
                cl.UpdateBuffer(indexBuffer, 0, indices);
            }
        }

        public void Render(CommandList cl, Range? primitiveRange = null, uint instanceStart = 1, uint instanceCount = 1)
        {
            Update(cl);
            var primsToRender = new RangeCollection
            {
                primitiveRange ?? Range.All
            };
            foreach (var free in freePrims)
                primsToRender.Remove(free);

            cl.SetVertexBuffer(0, VertexBuffer);
            cl.SetIndexBuffer(IndexBuffer, IndexFormat.UInt16);
            foreach (var toRender in primsToRender)
            {
                var (toRenderOff, toRenderCount) = toRender.GetOffsetAndLength(Capacity);
                cl.DrawIndexed(
                    indexStart: (uint)(toRenderOff * indexPattern.Length),
                    indexCount: (uint)(indexPattern.Length * toRenderCount),
                    instanceStart: instanceStart,
                    instanceCount: instanceCount,
                    vertexOffset: 0);
            }
        }

        private int GetVertexOffset(Index primIndex, Index vertexIndex)
        {
            var vertexOffset = vertexIndex.GetOffset(verticesPerPrimitive);
            if (vertexOffset < 0 || vertexOffset >= verticesPerPrimitive)
                throw new ArgumentOutOfRangeException(nameof(vertexIndex));

            var primOffset = primIndex.GetOffset(Capacity);
            if (primOffset < 0 || primOffset >= Capacity)
                throw new ArgumentOutOfRangeException(nameof(primOffset));

            return primOffset * verticesPerPrimitive + vertexOffset;
        }
    }
}
