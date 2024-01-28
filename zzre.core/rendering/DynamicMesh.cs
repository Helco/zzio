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
    private DeviceBuffer? indexBuffer;
    private int uploadedPrimitiveCount;
    private ushort[] indexPattern = Array.Empty<ushort>();
    private int verticesPerPrimitive;
    
    public int VertexCapacity => attributes.FirstOrDefault()?.Buffer.ReservedCapacity ?? 0;
    public int VertexCount => attributes.FirstOrDefault()?.Buffer.Count ?? 0;
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
    }

    protected override void DisposeManaged()
    {
        base.DisposeManaged();
        foreach (var attribute in attributes)
            attribute.Dispose();
        attributes.Clear();
        indexBuffer?.Dispose();
    }

    public void Clear()
    {
        foreach (var attribute in attributes)
            attribute.Buffer.Clear();
    }

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

    public void Update(CommandList cl)
    {
        foreach (var attribute in attributes)
            attribute.Buffer.Update(cl);
        UpdateIndexBuffer(cl);
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
}
