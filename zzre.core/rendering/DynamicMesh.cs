using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Linq;
using zzio;
using Veldrid;

namespace zzre.rendering;

public class DynamicMesh : BaseDisposable, IVertexAttributeContainer
{
    private const int MaxUploadDistance = 512;

    private interface IAttribute : IDisposable
    {
        string Name { get; }
        DynamicGraphicsBuffer Buffer { get; }
    }

    public class Attribute<T> : IAttribute where T : unmanaged
    {
        private readonly DynamicGraphicsBuffer buffer;
        public string Name { get; }
        DynamicGraphicsBuffer IAttribute.Buffer => buffer;

        public unsafe Attribute(GraphicsDevice device, bool dynamic, string meshName, string name, float minGrowFactor)
        {
            Name = name;
            buffer = new(device,
                BufferUsage.VertexBuffer | (dynamic ? BufferUsage.Dynamic : default),
                $"{meshName} {name}",
                minGrowFactor)
            {
                SizePerElement = sizeof(T)
            };
        }

        void IDisposable.Dispose() => buffer.Dispose();

        public ReadOnlySpan<T> Read(Range range) =>
            MemoryMarshal.Cast<byte, T>(buffer.Read(range));

        public Span<T> Write(Range range) =>
            MemoryMarshal.Cast<byte, T>(buffer.Write(range));

        public Span<T> Write(int offset, int count) =>
            Write(offset..(offset + count));

        public T this[int index]
        {
            get => Read(index..(index + 1))[0];
            set => Write(index..(index + 1))[0] = value;
        }
    }

    protected readonly GraphicsDevice graphicsDevice;
    protected readonly ResourceFactory resourceFactory;
    private readonly bool dynamic;
    private readonly string meshName;
    private readonly float minGrowFactor;
    private readonly List<IAttribute> attributes = new();
    private readonly DynamicGraphicsBuffer indexBuffer;

    public int VertexCapacity => attributes.FirstOrDefault()?.Buffer.ReservedCapacity ?? 0;
    public int VertexCount => attributes.FirstOrDefault()?.Buffer.Count ?? 0;
    public int IndexCapacity => indexBuffer.ReservedCapacity;
    public int IndexCount => indexBuffer.Count;
    public IndexFormat IndexFormat => IndexFormat.UInt16;
    public DeviceBuffer IndexBuffer => indexBuffer.Buffer;

    public DynamicMesh(ITagContainer diContainer,
        bool dynamic = true,
        string name = nameof(DynamicMesh),
        float minGrowFactor = 1.5f)
    {
        if (minGrowFactor <= 1f)
            throw new ArgumentOutOfRangeException(nameof(minGrowFactor));
        graphicsDevice = diContainer.GetTag<GraphicsDevice>();
        resourceFactory = graphicsDevice.ResourceFactory;
        this.dynamic = dynamic;
        this.meshName = name;
        this.minGrowFactor = minGrowFactor;

        indexBuffer = new(graphicsDevice,
            BufferUsage.IndexBuffer | (dynamic ? BufferUsage.Dynamic : default),
            name + " Indices",
            minGrowFactor)
        {
            SizePerElement = sizeof(ushort)
        };
    }

    protected override void DisposeManaged()
    {
        base.DisposeManaged();
        foreach (var attribute in attributes)
            attribute.Dispose();
        attributes.Clear();
        indexBuffer.Dispose();
    }

    public void Clear()
    {
        ClearVertices();
        ClearIndices();
    }

    public void ClearVertices()
    {
        foreach (var attribute in attributes)
            attribute.Buffer.Clear();
    }

    public void ClearIndices() => indexBuffer.Clear();

    public Range RentVertices(int request, bool fast = false)
    {
        if (!attributes.Any())
            throw new InvalidOperationException("Cannot rent any vertices without attributes");
        var range = attributes.First().Buffer.Rent(request, fast);
        foreach (var attribute in attributes.Skip(1))
        {
            var otherRange = attribute.Buffer.Rent(request, fast);
            if (!range.Equals(otherRange))
                throw new InvalidOperationException("Somehow the attribute buffers have gone out of sync");
        }
        return range;
    }

    public void ReturnVertices(Range range)
    {
        foreach (var attribute in attributes)
            attribute.Buffer.Return(range);
    }

    public Range RentIndices(int request, bool fast = false) =>
        indexBuffer.Rent(request, fast);

    public void ReturnIndices(Range range) =>
        indexBuffer.Return(range);

    public ReadOnlySpan<ushort> ReadIndices(Range range) =>
        MemoryMarshal.Cast<byte, ushort>(indexBuffer.Read(range));

    public Span<ushort> WriteIndices(Range range) =>
        MemoryMarshal.Cast<byte, ushort>(indexBuffer.Write(range));

    public void SetIndicesFromPattern(IReadOnlyList<ushort> pattern)
    {
        ClearIndices();
        var verticesPerPrimitive = pattern.Max() + 1;
        var primitiveCount = VertexCount / verticesPerPrimitive;
        if (primitiveCount <= 0)
            return;
        var indexRange = RentIndices(primitiveCount * pattern.Count);
        StaticMesh.GeneratePatternIndices(WriteIndices(indexRange), pattern, primitiveCount, verticesPerPrimitive);
    }

    public void Update(CommandList cl)
    {
        foreach (var attribute in attributes)
            attribute.Buffer.Update(cl);
        indexBuffer.Update(cl);
    }

    public bool TryGetBufferByMaterialName(string name, [NotNullWhen(true)] out DeviceBuffer? buffer, out uint offset)
    {
        var attribute = attributes.FirstOrDefault(a => a.Name == name);
        buffer = attribute?.Buffer.Buffer;
        offset = 0u;
        return buffer != null;
    }

    public Attribute<T> AddAttribute<T>(string attributeName) where T : unmanaged
    {
        var attribute = new Attribute<T>(graphicsDevice, dynamic, meshName, attributeName, minGrowFactor);
        attributes.Add(attribute);
        return attribute;
    }

    public void Preallocate(int vertices, int indices)
    {
        if (vertices < 0 || indices < 0)
            throw new ArgumentOutOfRangeException();
        if (vertices > 0)
        {
            if (VertexCount > 0)
                throw new InvalidOperationException("Cannot preallocate vertices while buffer is in use");
            var vertexRange = RentVertices(vertices);
            foreach (var attribute in attributes)
                attribute.Buffer.Write(vertexRange);
            ClearVertices();
        }
        if (indices > 0)
        {
            if (IndexCount > 0)
                throw new InvalidOperationException("Cannot preallocate indices while buffer is in use");
            var indexRange = RentIndices(indices);
            indexBuffer.Write(indexRange);
            ClearIndices();
        }
    }
}
