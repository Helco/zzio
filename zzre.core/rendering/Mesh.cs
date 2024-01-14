using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Veldrid;
using zzio;

namespace zzre.rendering;

public class Mesh : BaseDisposable
{
    public readonly record struct SubMesh(int IndexOffset, int IndexCount, int Material) : IComparable<SubMesh>
    {
        public int CompareTo(SubMesh other)
        {
            if (other.Material == Material)
                return IndexOffset - other.IndexOffset;
            return Material - other.Material;
        }
    }

    public class VertexAttribute
    {
        public required DeviceBuffer DeviceBuffer { get; init; }
        public required string DebugName { get; init; }
        public required string MaterialName { get; init; }
    }

    protected readonly GraphicsDevice graphicsDevice;
    protected readonly ResourceFactory resourceFactory;
    private readonly List<SubMesh> subMeshes = new();
    private readonly List<VertexAttribute> attributes = new();
    private DeviceBuffer? indexBuffer;

    public IReadOnlyList<SubMesh> SubMeshes => subMeshes;
    public IReadOnlyList<VertexAttribute> Attributes => attributes;
    public int VertexCount { get; private set; }
    public int IndexCount { get; private set; }
    public IndexFormat IndexFormat { get; private set; }
    public DeviceBuffer IndexBuffer => indexBuffer ??
        throw new InvalidOperationException("Index buffer was not yet set on mesh");
    public string Name { get; }

    public Mesh(ITagContainer diContainer, string name)
    {
        graphicsDevice = diContainer.GetTag<GraphicsDevice>();
        resourceFactory = graphicsDevice.ResourceFactory;
        Name = name;
    }

    protected override void DisposeManaged()
    {
        base.DisposeManaged();
        foreach (var attribute in attributes)
            attribute.DeviceBuffer.Dispose();
        attributes.Clear();
        indexBuffer?.Dispose();
    }

    private void CheckElementCountOfNewAttribute(string debugName, int elementCount)
    {
        if (elementCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(elementCount));
        if (VertexCount > 0 && VertexCount != elementCount)
            throw new ArgumentException($"Vertex count of attribute {debugName} does not match");
        if (VertexCount == 0)
            VertexCount = elementCount;
    }

    /// <remarks>Takes ownership over buffer</remarks>
    public VertexAttribute Add(string debugName, string materialName, DeviceBuffer buffer, int elementCount)
    {
        CheckElementCountOfNewAttribute(debugName, elementCount);
        attributes.Add(new()
        {
            DebugName = debugName,
            MaterialName = materialName,
            DeviceBuffer = buffer
        });
        return attributes.Last();
    }

    /// <remarks>Takes ownership over buffer</remarks>
    public VertexAttribute Add(string name, DeviceBuffer buffer, int elementCount) =>
        Add(name, name, buffer, elementCount);

    public VertexAttribute Add(string debugName, string materialName, int elementCount, int elementSize)
    {
        CheckElementCountOfNewAttribute(debugName, elementCount);
        var sizeInBytes = checked((uint)(elementCount * elementSize));
        var deviceBuffer = resourceFactory.CreateBuffer(new(sizeInBytes, BufferUsage.VertexBuffer));
        deviceBuffer.Name = $"{Name} {debugName}";
        attributes.Add(new()
        {
            DebugName = debugName,
            MaterialName = materialName,
            DeviceBuffer = deviceBuffer
        });
        return attributes.Last();
    }

    public VertexAttribute Add(string name, int elementCount, int elementSize) =>
        Add(name, name, elementCount, elementSize);

    public VertexAttribute Add<T>(string debugName, string materialName, T[] data) where T : unmanaged =>
        Add<T>(debugName, materialName, data.AsSpan());

    public unsafe VertexAttribute Add<T>(string debugName, string materialName, ReadOnlySpan<T> data) where T : unmanaged
    {
        var attribute = Add(debugName, materialName, data.Length, sizeof(T));
        graphicsDevice.UpdateBuffer(attribute.DeviceBuffer, 0u, data);
        return attribute;
    }

    public unsafe VertexAttribute Add<T>(string name, ReadOnlySpan<T> data) where T : unmanaged =>
        Add(name, name, data);

    public bool TryGetByMaterialName(string name, [NotNullWhen(true)] out VertexAttribute? attribute)
    {
        attribute = attributes.FirstOrDefault(a => a.MaterialName == name);
        return attribute != null;
    }

    public VertexAttribute GetByMaterialName(string name) =>
        TryGetByMaterialName(name, out var attribute) ? attribute
        : throw new KeyNotFoundException($"Attribute {name} is not present in mesh");

    public DeviceBuffer SetIndexCount(int indexCount, IndexFormat format)
    {
        if (subMeshes.Any(m => m.IndexOffset + m.IndexCount > indexCount))
            throw new ArgumentException("Mesh contains submesh with higher index count than requested");
        indexBuffer?.Dispose();
        var indexSize = format switch
        {
            IndexFormat.UInt16 => sizeof(ushort),
            IndexFormat.UInt32 => sizeof(uint),
            _ => throw new NotSupportedException($"Unsupported index format {format}")
        };
        var sizeInBytes = checked((uint)(indexSize * indexCount));
        indexBuffer = resourceFactory.CreateBuffer(new(sizeInBytes, BufferUsage.IndexBuffer));
        indexBuffer.Name = $"{Name} Indices";
        IndexFormat = format;
        IndexCount = indexCount;
        return indexBuffer;
    }

    public void SetIndices(ReadOnlySpan<ushort> indices)
    {
        var buffer = SetIndexCount(indices.Length, IndexFormat.UInt16);
        graphicsDevice.UpdateBuffer(buffer, 0u, indices);
    }

    public void SetIndices(ReadOnlySpan<uint> indices)
    {
        var buffer = SetIndexCount(indices.Length, IndexFormat.UInt32);
        graphicsDevice.UpdateBuffer(buffer, 0u, indices);
    }

    public void AddSubMesh(SubMesh subMesh)
    {
        if (IndexCount <= 0)
            throw new InvalidOperationException("Cannot set sub meshes before indices");
        if (subMesh.IndexOffset + subMesh.IndexCount > IndexCount)
            throw new ArgumentException("Cannot set submesh with higher index count");
        var index = subMeshes.BinarySearch(subMesh);
        if (index < 0)
            index = ~index;
        else
            index++;
        subMeshes.Insert(index, subMesh);
    }

    public void AddSubMesh(int indexOffset, int indexCount, int material = 0) =>
        AddSubMesh(new(indexOffset, indexCount, material));
}
