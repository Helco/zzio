﻿using System;
using System.Numerics;
using Veldrid;
using zzio;
using zzre.materials;

namespace zzre.rendering;

public struct ModelInstance2
{
    public Matrix4x4 world;
    public Matrix3x2 texShift;
    public IColor tint;
    public float vertexColorFactor;
    public float tintFactor;
    public float alphaReference;
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

    public void Add(ModelInstance2 i)
    {
        var index = Add(1);
        GetAttributeData<Matrix4x4>(attrWorld)[index] = i.world;
        GetAttributeData<Matrix3x2>(attrTexShift)[index] = i.texShift;
        GetAttributeData<IColor>(attrTint)[index] = i.tint;
        GetAttributeData<float>(attrVertexColorFactor)[index] = i.vertexColorFactor;
        GetAttributeData<float>(attrTintFactor)[index] = i.tintFactor;
        GetAttributeData<float>(attrAlphaReference)[index] = i.alphaReference;
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
