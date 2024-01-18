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
    private interface IAttribute
    {
        string Name { get; }
        uint Offset { get; set; }
        uint ElementSize { get; }
    }

    public class Attribute<T> : IAttribute where T : unmanaged
    {
        private readonly DynamicMesh mesh;
        public string Name { get; }
        uint IAttribute.Offset { get; set; }
        unsafe uint IAttribute.ElementSize => (uint)sizeof(T);

        public Attribute(DynamicMesh mesh, string name)
        {
            this.mesh = mesh;
            Name = name;
        }

        public T this[int index]
        {
            get => ReadSpan[index];
            set => AsWriteSpan(index, 1)[0] = value;
        }

        public ReadOnlySpan<T> ReadSpan => AsSpanInternal(forWrite: false, 0, -1);
        public Span<T> AsWriteSpan(int offset = 0, int length = -1) => AsSpanInternal(forWrite: true, offset, length);

        private Span<T> AsSpanInternal(bool forWrite, int offset, int length)
        {
            mesh.EnsureArray();
            var byteRange = mesh.GetByteRange(this, offset, length);
            if (forWrite)
                mesh.dirtyVertexBytes.Add(byteRange);
            return MemoryMarshal.Cast<byte, T>(mesh.vertices!.AsSpan(byteRange));
        }
    }

    protected readonly GraphicsDevice graphicsDevice;
    protected readonly ResourceFactory resourceFactory;
    private readonly bool dynamic;
    private readonly List<IAttribute> attributes = new();
    private readonly RangeCollection dirtyVertexBytes = new();
    private DeviceBuffer? vertexBuffer, indexBuffer;
    private Range dirtyIndices;
    private ushort[] indexPattern = Array.Empty<ushort>();
    private int verticesPerPrimitive;
    private byte[]? vertices;
    private int? nextVertexCapacity;
    
    private uint BytesPerVertex => attributes.Aggregate(0u, (t, a) => t + a.ElementSize);
    public int VertexCapacity { get; private set; }
    public int VertexCount { get; private set; }
    public int VertexFreeCount => VertexCapacity - VertexCount;
    public int PrimitiveCount => verticesPerPrimitive < 1 ? 0 : VertexCount / verticesPerPrimitive;

    public IReadOnlyList<ushort> IndexPattern
    {
        get => indexPattern;
        set
        {
            indexPattern = value.ToArray();
            dirtyIndices = Range.All;
            verticesPerPrimitive = indexPattern.Max() + 1;
        }
    }
    public IndexFormat IndexFormat => IndexFormat.UInt16;

    public DynamicMesh(ITagContainer diContainer, bool dynamic = true)
    {
        graphicsDevice = diContainer.GetTag<GraphicsDevice>();
        resourceFactory = graphicsDevice.ResourceFactory;
        this.dynamic = dynamic;
    }

    protected override void DisposeManaged()
    {
        base.DisposeManaged();
        ResetBuffer();
        attributes.Clear();
    }

    private void ResetBuffer()
    {
        vertexBuffer?.Dispose();
        vertexBuffer = null;
        vertices = null;
        VertexCapacity = VertexCount = 0;
    }

    public void Clear() => VertexCount = 0;

    public void Reserve(int capacity)
    {
        if (VertexCount > 0)
            throw new InvalidOperationException("Cannot resize dynamic mesh during rendering");
        if (attributes.Count == 0)
            throw new InvalidOperationException("Cannot resize dynamic mesh without attributes");
        nextVertexCapacity ??= 0;
        nextVertexCapacity += capacity;
    }

    private void ApplyNextCapacity()
    {
        if (VertexCount > 0)
            throw new InvalidOperationException("Cannot resize dynamic mesh during rendering");
        if (attributes.Count == 0)
            throw new InvalidOperationException("Cannot resize dynamic mesh without attributes");
        if (nextVertexCapacity is null || VertexCapacity >= nextVertexCapacity.Value)
            return;
        VertexCapacity = nextVertexCapacity.Value;
        nextVertexCapacity = null;
        vertexBuffer?.Dispose();
        var totalSize = BytesPerVertex * (uint)VertexCapacity;
        var bufferUsage = BufferUsage.VertexBuffer | (dynamic ? BufferUsage.Dynamic : default);
        vertexBuffer = resourceFactory.CreateBuffer(new(totalSize, bufferUsage));
        vertexBuffer.Name = $"InstanceBuffer {GetHashCode()}";
        vertices = new byte[totalSize];

        var curOffset = 0u;
        for (int i = 0; i < attributes.Count; i++)
        {
            attributes[i] = attributes[i] with { Offset = curOffset };
            curOffset += attributes[i].ElementSize * (uint)VertexCapacity;
        }
    }

    public int Add(int count = 1)
    {
        ApplyNextCapacity();
        if (count < 1)
            throw new ArgumentOutOfRangeException(nameof(count));
        if (count == 0)
            return VertexCount - 1;
        if (count > VertexFreeCount)
            throw new ArgumentOutOfRangeException($"Dynamic mesh does not have enough capacity for further {count} instances (only {VertexFreeCount})");
        int result = VertexCount;
        VertexCount += count;
        return result;
    }

    public void Update(CommandList cl)
    {
        if (vertexBuffer == null || vertices == null)
            return;
        if (VertexFreeCount == 0)
            cl.UpdateBuffer(vertexBuffer, 0u, vertices);
        else
        {
            foreach (var attribute in attributes)
                cl.UpdateBuffer(vertexBuffer, attribute.Offset, ref vertices[attribute.Offset], (uint)(VertexCount * attribute.ElementSize));
        }
    }

    public bool TryGetBufferByMaterialName(string name, [NotNullWhen(true)] out DeviceBuffer? buffer, out uint offset)
    {
        ApplyNextCapacity();
        var attribute = attributes.FirstOrDefault(a => a.Name == name);
        buffer = this.vertexBuffer;
        offset = attribute.Offset;
        return attribute.Name != null;
    }

    public unsafe int AddAttribute<T>(string name) where T : unmanaged =>
        AddAttribute(name, (uint)sizeof(T));

    public int AddAttribute(string name, uint elementSize)
    {
        attributes.Add(new()
        {
            Name = name,
            ElementSize = elementSize,
            Offset = BytesPerVertex * (uint)VertexCapacity
        });
        ResetBuffer();
        return attributes.Count - 1;
    }

    public unsafe Span<T> GetAttributeData<T>(int attributeI) where T : unmanaged
    {
        ApplyNextCapacity();
        var a = attributes[attributeI];
        if (sizeof(T) != a.ElementSize)
            throw new ArgumentException("Given type does not match the registered one");
        if (vertices == null || VertexCount == 0)
            return Span<T>.Empty;
        var start = (int)a.Offset;
        var end = start + VertexCount * (int)a.ElementSize;
        return MemoryMarshal.Cast<byte, T>(vertices.AsSpan(start..end));
    }
}
