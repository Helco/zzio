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

public class UIInstanceBufferBase<TPosition> : DynamicMesh where TPosition : unmanaged
{
    protected readonly Attribute<TPosition> attrPos;
    protected readonly Attribute<Vector2>
        attrSize,
        attrUVPos,
        attrUVSize;
    protected readonly Attribute<float> attrTexWeight;
    protected readonly Attribute<IColor> attrColor;

    public UIInstanceBufferBase(ITagContainer diContainer, bool dynamic = true) : base(diContainer, dynamic)
    {
        attrPos = AddAttribute<TPosition>("inPos");
        attrSize = AddAttribute<Vector2>("inSize");
        attrUVPos = AddAttribute<Vector2>("inUVPos");
        attrUVSize = AddAttribute<Vector2>("inUVSize");
        attrColor = AddAttribute<IColor>("inColor");
        attrTexWeight = AddAttribute<float>("inTexWeight");
    }
}

public class UIInstanceBuffer : UIInstanceBufferBase<Vector2>
{
    public UIInstanceBuffer(ITagContainer diContainer, bool dynamic = true) : base(diContainer, dynamic) { }

    public void Add(UIInstance i)
    {
        var index = Add(1);
        attrPos[index] = i.pos;
        attrSize[index] = i.size;
        attrUVPos[index] = i.uvPos;
        attrUVSize[index] = i.uvSize;
        attrTexWeight[index] = i.textureWeight;
        attrColor[index] = i.color;
    }
}

public class DebugIconInstanceBuffer : UIInstanceBufferBase<Vector3>
{
    public DebugIconInstanceBuffer(ITagContainer diContainer, bool dynamic = true) : base(diContainer, dynamic) { }

    public void Add(DebugIcon i)
    {
        var index = Add(1);
        attrPos[index] = i.pos;
        attrSize[index] = i.size;
        attrUVPos[index] = i.uvPos;
        attrUVSize[index] = i.uvSize;
        attrTexWeight[index] = i.textureWeight;
        attrColor[index] = i.color;
    }
}
