using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.InteropServices;
using Veldrid;
using zzio;

namespace zzre.rendering;

public class StaticMesh : BaseDisposable, IVertexAttributeContainer
{
    public readonly record struct SubMesh(int IndexOffset, int IndexCount, int Material, int Section = 0);

    public sealed class SubMeshComparerByMaterial : IComparer<SubMesh>
    {
        public int Compare(SubMesh x, SubMesh y)
        {
            if (y.Material != x.Material)
                return x.Material - y.Material;
            if (y.Section != x.Section)
                return x.Section - y.Section;
            return x.IndexOffset - y.IndexOffset;
        }
    }

    public sealed class SubMeshComparerBySection : IComparer<SubMesh>
    {
        public int Compare(SubMesh x, SubMesh y)
        {
            if (y.Section != x.Section)
                return x.Section - y.Section;
            if (y.Material != x.Material)
                return x.Material - y.Material;
            return x.IndexOffset - y.IndexOffset;
        }
    }
    public static readonly SubMeshComparerByMaterial CompareByMaterial = new();
    public static readonly SubMeshComparerBySection CompareBySection = new();

    public class VertexAttribute
    {
        public required DeviceBuffer DeviceBuffer { get; init; }
        public required string DebugName { get; init; }
        public required string MaterialName { get; init; }
    }

    protected readonly GraphicsDevice graphicsDevice;
    protected readonly ResourceFactory resourceFactory;
    private readonly List<SubMesh> subMeshes = [];
    private readonly List<VertexAttribute> attributes = [];
    private DeviceBuffer? indexBuffer;

    public IReadOnlyList<SubMesh> SubMeshes => subMeshes;
    public IReadOnlyList<VertexAttribute> Attributes => attributes;
    public int VertexCount { get; private set; }
    public int IndexCount { get; private set; }
    public IndexFormat IndexFormat { get; private set; }
    public DeviceBuffer IndexBuffer => indexBuffer ??
        throw new InvalidOperationException("Index buffer was not yet set on mesh");
    public string Name { get; }

    public StaticMesh(ITagContainer diContainer, string name)
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
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(elementCount);
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
        foreach (var curAttribute in attributes)
        {
            if (curAttribute.MaterialName == name)
            {
                attribute = curAttribute;
                return true;
            }
        }
        attribute = null;
        return false;
    }

    public VertexAttribute GetByMaterialName(string name) =>
        TryGetByMaterialName(name, out var attribute) ? attribute
        : throw new KeyNotFoundException($"Attribute {name} is not present in mesh");

    public bool TryGetBufferByMaterialName(string name, [NotNullWhen(true)] out DeviceBuffer? attributeBuffer, out uint offset)
    {
        var result = TryGetByMaterialName(name, out var attribute);
        attributeBuffer = attribute?.DeviceBuffer;
        offset = 0u;
        return result;
    }

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

    public void SetIndicesFromPattern(IReadOnlyList<ushort> pattern)
    {
        if (VertexCount <= 0)
            throw new InvalidOperationException("Cannot generate indices from pattern without vertices");
        var verticesPerPrimitive = pattern.Max() + 1;
        var primitiveCount = VertexCount / verticesPerPrimitive;
        var buffer = SetIndexCount(primitiveCount * pattern.Count, IndexFormat.UInt16);
        var indices = new ushort[IndexCount];
        GeneratePatternIndices(indices, pattern, primitiveCount, verticesPerPrimitive);
        graphicsDevice.UpdateBuffer(buffer, 0u, indices);
    }

    public static void GeneratePatternIndices(
        Span<ushort> indices,
        IReadOnlyList<ushort> pattern,
        int primitiveCount,
        int verticesPerPrimitive,
        int vertexOffset = 0)
    {
        if (pattern.Count == 0)
            throw new ArgumentOutOfRangeException(nameof(pattern));
        if (primitiveCount <= 0)
            return;
        for (int i = 0; i < primitiveCount; i++)
        {
            for (int j = 0; j < pattern.Count; j++)
                indices[i * pattern.Count + j] = (ushort)(i * verticesPerPrimitive + pattern[j] + vertexOffset);
        }
    }

    public void AddSubMesh(SubMesh subMesh)
    {
        if (IndexCount <= 0)
            throw new InvalidOperationException("Cannot set sub meshes before indices");
        if (subMesh.IndexOffset + subMesh.IndexCount > IndexCount)
            throw new ArgumentException("Cannot set submesh with higher index count");
        var index = subMeshes.BinarySearch(subMesh, CompareBySection);
        if (index < 0)
            index = ~index;
        else
            index++;
        subMeshes.Insert(index, subMesh);
    }

    public void AddSubMesh(int indexOffset, int indexCount, int material = 0, int section = 0) =>
        AddSubMesh(new(indexOffset, indexCount, material, section));

    public Range GetSubMeshRange(int? section = null, int? material = null)
    {
        if (section is null && material is not null)
            throw new ArgumentException($"Cannot get submesh range for material filter without section filter");
            
        if (subMeshes.Count == 0)
            return 0..0;
        int first = 0, last = subMeshes.Count - 1;
        if (section.HasValue)
        {
            first = subMeshes.FindIndex(first, (last - first + 1), m => m.Section == section);
            if (first < 0)
                return 0..0;
            last = subMeshes.FindLastIndex(last, (last - first + 1), m => m.Section == section);
        }
        if (material.HasValue)
        {
            first = subMeshes.FindIndex(first, (last - first + 1), m => m.Material == material);
            if (first < 0)
                return 0..0;
            last = subMeshes.FindLastIndex(last, (last - first + 1), m => m.Material == material);
        }
        return first..(last + 1);
    }

    public ReadOnlySpan<SubMesh> GetSubMeshes(int? section = null, int? material = null)
    {
        var range = GetSubMeshRange(section, material);
        if (range.Start.Value == range.End.Value)
            return [];
        return CollectionsMarshal.AsSpan(subMeshes)[range];
    }

    public IEnumerable<SubMesh> GetSubMeshesLEGACY(int? section = null, int? material = null) =>
        subMeshes.Range(GetSubMeshRange(section, material));
}
