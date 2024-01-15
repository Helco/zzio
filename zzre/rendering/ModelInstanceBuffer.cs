using System;
using System.Numerics;
using Veldrid;
using zzio;
using zzre.materials;

namespace zzre.rendering;

public ref struct ModelInstanceRef
{
    public readonly ref Matrix4x4 World;
    public readonly ref Matrix3x2 TexShift;
    public readonly ref IColor Tint;
    public readonly ref float VertexColorFactor;
    public readonly ref float TintFactor;
    public readonly ref float AlphaReference;

    public ModelInstanceRef(
        ref Matrix4x4 world,
        ref Matrix3x2 texShift,
        ref IColor tint,
        ref float vertexColorFactor,
        ref float tintFactor,
        ref float alphaReference)
    {
        World = ref world;
        TexShift = ref texShift;
        Tint = ref tint;
        VertexColorFactor = ref vertexColorFactor;
        TintFactor = ref tintFactor;
        AlphaReference = ref alphaReference;
    }
}

public class ModelInstanceBuffer : InstanceBuffer
{
    private readonly int
        attrWorld,
        attrTexShift,
        attrTint,
        attrVertexColorFactor,
        attrTintFactor,
        attrAlphaReference;

    public ModelInstanceBuffer(ITagContainer diContainer, bool dynamic = true) : base(diContainer, dynamic)
    {
        attrWorld = AddAttribute<Matrix4x4>("world");
        attrTexShift = AddAttribute<Matrix3x2>("inTexShift");
        attrTint = AddAttribute<IColor>("inTint");
        attrVertexColorFactor = AddAttribute<float>("inVertexColorFactor");
        attrTintFactor = AddAttribute<float>("inTintFactor");
        attrAlphaReference = AddAttribute<float>("inAlphaReference");
    }

    public ModelInstanceRef Add()
    {
        var index = Add(1);
        return new(
            ref GetAttributeData<Matrix4x4>(attrWorld)[index],
            ref GetAttributeData<Matrix3x2>(attrTexShift)[index],
            ref GetAttributeData<IColor>(attrTint)[index],
            ref GetAttributeData<float>(attrVertexColorFactor)[index],
            ref GetAttributeData<float>(attrTintFactor)[index],
            ref GetAttributeData<float>(attrAlphaReference)[index]);
    }
}

public class ModelInstanceBufferLEGACY : BaseDisposable
{
    public DeviceBuffer DeviceBuffer { get; }
    public int TotalCount { get; }
    public int FreeCount { get; private set; }

    public ModelInstanceBufferLEGACY(ITagContainer diContainer, int totalCount, bool dynamic)
    {
        var resourceFactory = diContainer.GetTag<ResourceFactory>();
        var usageFlags = BufferUsage.VertexBuffer |
            (dynamic ? BufferUsage.Dynamic : default);
        DeviceBuffer = resourceFactory.CreateBuffer(new BufferDescription(
            ModelInstance.Stride * (uint)totalCount, usageFlags));
        DeviceBuffer.Name = nameof(ModelInstanceBufferLEGACY);
        TotalCount = totalCount;
        FreeCount = totalCount;
    }

    protected override void DisposeManaged()
    {
        base.DisposeManaged();
        DeviceBuffer.Dispose();
    }

    public void Clear() => FreeCount = TotalCount;

    public uint Reserve(int count)
    {
        if (count > FreeCount)
            throw new ArgumentOutOfRangeException($"ModelInstanceBuffer does not have enough capacity for {count} instances (only {FreeCount})");
        return (uint)(TotalCount - FreeCount);
    }
}
