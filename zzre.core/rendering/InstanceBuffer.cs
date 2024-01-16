using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Linq;
using zzio;
using Veldrid;

namespace zzre.rendering;

public class InstanceBuffer : BaseDisposable, IVertexAttributeContainer
{
    private struct Attribute
    {
        public string? Name;
        public uint Offset;
        public uint ElementSize;
    }

    protected readonly GraphicsDevice graphicsDevice;
    protected readonly ResourceFactory resourceFactory;
    private readonly bool dynamic;
    private readonly List<Attribute> attributes = new();
    private DeviceBuffer? buffer;
    private byte[]? bytes;
    
    public int Capacity { get; private set; }
    public int Count { get; private set; }
    public int FreeCount => Capacity - Count;
    private uint BytesPerInstance => attributes.Aggregate(0u, (t, a) => t + a.ElementSize);

    public InstanceBuffer(ITagContainer diContainer, bool dynamic = true)
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
        buffer?.Dispose();
        buffer = null;
        bytes = null;
        Capacity = Count = 0;
    }

    public void Clear() => Count = 0;

    public void Ensure(int capacity, bool shrinkToFit = false)
    {
        if (capacity <= Capacity && !shrinkToFit)
            return;
        if (Count > 0)
            throw new InvalidOperationException("Cannot resize instance buffer during rendering");
        if (attributes.Count == 0)
            throw new InvalidOperationException("Cannot resize instance buffer without attributes");
        Capacity = capacity;
        buffer?.Dispose();
        var totalSize = BytesPerInstance * (uint)Capacity;
        var bufferUsage = BufferUsage.VertexBuffer | (dynamic ? BufferUsage.Dynamic : default);
        buffer = resourceFactory.CreateBuffer(new(totalSize, bufferUsage));
        buffer.Name = $"InstanceBuffer {GetHashCode()}";
        bytes = new byte[totalSize];

        var curOffset = 0u;
        for (int i = 0; i < attributes.Count; i++)
        {
            attributes[i] = attributes[i] with { Offset = curOffset };
            curOffset += attributes[i].ElementSize * (uint)capacity;
        }
    }

    public int Add(int count = 1)
    {
        if (count < 1)
            throw new ArgumentOutOfRangeException(nameof(count));
        if (count == 0)
            return Count - 1;
        if (count > FreeCount)
            throw new ArgumentOutOfRangeException($"InstanceBuffer does not have enough capacity for further {count} instances (only {FreeCount})");
        int result = Count;
        Count += count;
        return result;
    }

    public void Update(CommandList cl)
    {
        if (buffer == null || bytes == null)
            return;
        if (FreeCount == 0)
            cl.UpdateBuffer(buffer, 0u, bytes);
        else
        {
            foreach (var attribute in attributes)
                cl.UpdateBuffer(buffer, attribute.Offset, ref bytes[attribute.Offset], (uint)(Count * attribute.ElementSize));
        }
    }

    public bool TryGetBufferByMaterialName(string name, [NotNullWhen(true)] out DeviceBuffer? buffer, out uint offset)
    {
        var attribute = attributes.FirstOrDefault(a => a.Name == name);
        buffer = this.buffer;
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
            Offset = BytesPerInstance * (uint)Capacity
        });
        ResetBuffer();
        return attributes.Count - 1;
    }

    public unsafe Span<T> GetAttributeData<T>(int attributeI) where T : unmanaged
    {
        var a = attributes[attributeI];
        if (sizeof(T) != a.ElementSize)
            throw new ArgumentException("Given type does not match the registered one");
        if (bytes == null || Count == 0)
            return Span<T>.Empty;
        var start = (int)a.Offset;
        var end = start + Count * (int)a.ElementSize;
        return MemoryMarshal.Cast<byte, T>(bytes.AsSpan(start..end));
    }
}
