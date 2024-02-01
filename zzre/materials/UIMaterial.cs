using System;
using System.Buffers;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using Veldrid;
using zzio;
using zzre.rendering;

namespace zzre.materials;

public struct UIInstance
{
    public Vector2 pos, size;
    public Vector2 uvPos, uvSize;
    public float textureWeight;
    public IColor color;
}

public struct DebugIcon
{
    public Vector3 pos;
    public Vector2 size;
    public Vector2 uvPos, uvSize;
    public float textureWeight;
    public IColor color;
}

public class UIMaterial : MlangMaterial, IStandardTransformMaterial
{
    public bool IsInstanced { set => SetOption(nameof(IsInstanced), value); }
    public bool IsFont { set => SetOption(nameof(IsFont), value); }
    public bool Is3D { set => SetOption(nameof(Is3D), value); }
    public bool HasMask { set => SetOption(nameof(HasMask), value); }

    public TextureBinding MainTexture { get; }
    public SamplerBinding MainSampler { get; }
    public UniformBinding<Vector2> ScreenSize { get; }
    public UniformBinding<Matrix4x4> Projection { get; }
    public UniformBinding<Matrix4x4> View { get; }
    public UniformBinding<Matrix4x4> World { get; }
    public TextureBinding MaskTexture { get; }
    public SamplerBinding MaskSampler { get; }
    public UniformBinding<uint> MaskBits { get; }

    public UIMaterial(ITagContainer diContainer) : base(diContainer, "ui")
    {
        IsInstanced = true;
        AddBinding("mainTexture", MainTexture = new(this));
        AddBinding("mainSampler", MainSampler = new(this));
        AddBinding("screenSize", ScreenSize = new(this));
        AddBinding("projection", Projection = new(this));
        AddBinding("view", View = new(this));
        AddBinding("world", World = new(this));
        AddBinding("maskTexture", MaskTexture = new(this));
        AddBinding("maskSampler", MaskSampler = new(this));
        AddBinding("maskBits", MaskBits = new(this));
    }
}

public abstract class UIInstanceBufferBase<TPosition> : DynamicMesh where TPosition : unmanaged
{
    public readonly Attribute<TPosition> AttrPos;
    public readonly Attribute<Vector2>
        AttrSize,
        AttrUVPos,
        AttrUVSize;
    public readonly Attribute<float> AttrTexWeight;
    public readonly Attribute<IColor> AttrColor;

    public UIInstanceBufferBase(ITagContainer diContainer,
        bool dynamic = true,
        string name = nameof(UIInstanceBuffer))
        : base(diContainer, dynamic, name)
    {
        AttrPos = AddAttribute<TPosition>("inPos");
        AttrSize = AddAttribute<Vector2>("inSize");
        AttrUVPos = AddAttribute<Vector2>("inUVPos");
        AttrUVSize = AddAttribute<Vector2>("inUVSize");
        AttrColor = AddAttribute<IColor>("inColor");
        AttrTexWeight = AddAttribute<float>("inTexWeight");
    }
}

public sealed class UIInstanceBuffer : UIInstanceBufferBase<Vector2>
{
    public UIInstanceBuffer(ITagContainer diContainer,
        bool dynamic = true,
        string name = nameof(UIInstanceBuffer))
        : base(diContainer, dynamic, name) { }
}

public sealed class DebugIconInstanceBuffer : UIInstanceBufferBase<Vector3>
{
    public DebugIconInstanceBuffer(ITagContainer diContainer,
        bool dynamic = true,
        string name = nameof(DebugIconInstanceBuffer))
        : base(diContainer, dynamic, name) { }

    public void AddRange(IReadOnlyCollection<DebugIcon> instances)
    {
        var index = RentVertices(instances.Count).Start.Value;
        foreach (var i in instances)
        {
            AttrPos[index] = i.pos;
            AttrSize[index] = i.size;
            AttrUVPos[index] = i.uvPos;
            AttrUVSize[index] = i.uvSize;
            AttrTexWeight[index] = i.textureWeight;
            AttrColor[index] = i.color;
            index++;
        }
    }
}
