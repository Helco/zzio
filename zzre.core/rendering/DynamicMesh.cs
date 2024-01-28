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
    private readonly float minGrowFactor;
    private readonly List<IAttribute> attributes = new();
    private readonly RangeCollection dirtyVertexBytes = new();
    private DeviceBuffer? vertexBuffer, indexBuffer;
    private int uploadedPrimitiveCount;
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
            uploadedPrimitiveCount = 0;
            verticesPerPrimitive = indexPattern.Max() + 1;
        }
    }
    public int IndexCount => PrimitiveCount * IndexPattern.Count;
    public IndexFormat IndexFormat => IndexFormat.UInt16;
    public DeviceBuffer IndexBuffer => indexBuffer ??
        throw new InvalidOperationException("Index buffer was not yet generated");

    public DynamicMesh(ITagContainer diContainer, bool dynamic = true, float minGrowFactor = 1.5f)
    {
        if (minGrowFactor <= 1f)
            throw new ArgumentOutOfRangeException(nameof(minGrowFactor));
        graphicsDevice = diContainer.GetTag<GraphicsDevice>();
        resourceFactory = graphicsDevice.ResourceFactory;
        this.dynamic = dynamic;
        this.minGrowFactor = minGrowFactor;
    }

    protected override void DisposeManaged()
    {
        base.DisposeManaged();
        vertexBuffer?.Dispose();
        indexBuffer?.Dispose();
        vertices = null;
        attributes.Clear();
    }

    public void Clear() => VertexCount = 0;

    public void Reserve(int capacity, bool additive = true)
    {
        if (attributes.Count == 0)
            throw new InvalidOperationException("Cannot resize dynamic mesh without attributes");
        if (capacity < 0)
            throw new ArgumentOutOfRangeException(nameof(capacity));
        if (additive)
            nextVertexCapacity ??= VertexCapacity;
        else
            nextVertexCapacity = 0;
        nextVertexCapacity += capacity;
    }

    public int Add(int count = 1)
    {
        if (count < 1)
            throw new ArgumentOutOfRangeException(nameof(count));
        if (count == 0)
            return VertexCount - 1;
        if (count > VertexFreeCount)
        {
            if (nextVertexCapacity is not null)
                nextVertexCapacity = Math.Max(nextVertexCapacity.Value, VertexCapacity);
            Reserve(count - VertexFreeCount, additive: true);
        }
        EnsureArray();
        int result = VertexCount;
        VertexCount += count;
        return result;
    }

    public void EnsureArray()
    {
        if (attributes.Count == 0)
            throw new InvalidOperationException("Cannot resize dynamic mesh without attributes");

        if ((vertices?.LongLength ?? 0) >= BytesPerVertex * VertexCapacity &&
            VertexCapacity >= nextVertexCapacity)
        {
            nextVertexCapacity = null;
            return;
        }

        if (nextVertexCapacity is not null)
        {
            var minNextVertexCapacity = (int)(VertexCapacity * minGrowFactor + 0.5f);
            VertexCapacity = Math.Max(minNextVertexCapacity, nextVertexCapacity.Value);
        }
        nextVertexCapacity = null;

        var needsCopyFromPrevious = vertices != null && VertexCount > 0;
        var newVertices = new byte[VertexCapacity * BytesPerVertex];
        var curOffset = 0u;
        for (int i = 0; i < attributes.Count; i++)
        {
            if (needsCopyFromPrevious && attributes[i].Offset < vertices!.Length)
                vertices.AsSpan(GetByteRange(attributes[i])).CopyTo(newVertices.AsSpan((int)curOffset));
            attributes[i].Offset = curOffset;
            curOffset += attributes[i].ElementSize * (uint)VertexCapacity;
        }
        vertices = newVertices;

        if (needsCopyFromPrevious)
        {
            dirtyVertexBytes.Clear();
            dirtyVertexBytes.MaxRangeValue = vertices.Length;
            dirtyVertexBytes.Add(Range.All);
        }
        else
            dirtyVertexBytes.MaxRangeValue = vertices.Length;
    }

    public void Update(CommandList cl)
    {
        UpdateVertexBuffer(cl);
        UpdateIndexBuffer(cl);
    }

    private void UpdateVertexBuffer(CommandList cl)
    {
        if (vertices == null)
            return;
        if ((vertexBuffer?.SizeInBytes ?? 0) < vertices.Length)
        {
            if (vertexBuffer != null)
            {
                dirtyVertexBytes.Clear();
                dirtyVertexBytes.Add(Range.All);
            }
            vertexBuffer?.Dispose();
            var bufferUsage = BufferUsage.VertexBuffer | (dynamic ? BufferUsage.Dynamic : default);
            vertexBuffer = resourceFactory.CreateBuffer(new((uint)vertices.Length, bufferUsage));
            vertexBuffer.Name = $"InstanceBuffer {GetHashCode()}";
        }

        dirtyVertexBytes.MergeNearbyRanges(MaxUploadDistance);
        foreach (var range in dirtyVertexBytes)
        {
            var offset = range.GetOffset((int)vertexBuffer!.SizeInBytes);
            cl.UpdateBuffer(vertexBuffer, (uint)offset, vertices.AsSpan(range));
        }
        dirtyVertexBytes.Clear();
    }

    private void UpdateIndexBuffer(CommandList cl)
    {
        if (IndexPattern.Count == 0)
            return;
        var expectedIndexBufferSize = PrimitiveCount * IndexPattern.Count * sizeof(ushort);
        var indexBufferTooSmall = (indexBuffer?.SizeInBytes ?? 0) < expectedIndexBufferSize;

        if (indexBufferTooSmall)
        {
            uploadedPrimitiveCount = 0;
            indexBuffer?.Dispose();
            indexBuffer = resourceFactory.CreateBuffer(new((uint)expectedIndexBufferSize, BufferUsage.IndexBuffer));
            indexBuffer.Name = $"InstanceBuffer Indices {GetHashCode()}";
        }

        if (uploadedPrimitiveCount < PrimitiveCount)
        {
            var indices = StaticMesh.GeneratePatternIndices(indexPattern, uploadedPrimitiveCount, PrimitiveCount, verticesPerPrimitive);
            cl.UpdateBuffer(indexBuffer, (uint)(uploadedPrimitiveCount * PrimitiveCount * sizeof(ushort)), indices);
        }
    }

    public bool TryGetBufferByMaterialName(string name, [NotNullWhen(true)] out DeviceBuffer? buffer, out uint offset)
    {
        var attribute = attributes.FirstOrDefault(a => a.Name == name);
        buffer = vertexBuffer;
        offset = attribute?.Offset ?? 0u;
        return attribute?.Name != null && buffer != null;
    }

    public Attribute<T> AddAttribute<T>(string name) where T : unmanaged
    {
        var attribute = new Attribute<T>(this, name);
        attributes.Add(attribute);
        return attribute;
    }

    private Range GetByteRange(IAttribute attribute, int offset = 0, int length = -1)
    {
        if (length < 0)
            length = VertexCount - offset;
        if (offset < 0 || offset + length > VertexCount)
            throw new ArgumentOutOfRangeException(nameof(offset));
        var start = attribute.Offset + offset * attribute.ElementSize;
        var end = attribute.Offset + (offset + length) * attribute.ElementSize;
        return (int)start..(int)end;
    }
}
