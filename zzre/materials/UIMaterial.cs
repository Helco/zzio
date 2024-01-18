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

public class UIMaterial : MlangMaterial
{
    public bool IsInstanced { set => SetOption(nameof(IsInstanced), value); }
    public bool IsFont { set => SetOption(nameof(IsFont), value); }

    public TextureBinding Texture { get; }
    public SamplerBinding Sampler { get; }
    public UniformBinding<Vector2> ScreenSize { get; }

    public UIMaterial(ITagContainer diContainer) : base(diContainer, "ui")
    {
        IsInstanced = true;
        AddBinding("mainTexture", Texture = new(this));
        AddBinding("mainSampler", Sampler = new(this));
        AddBinding("screenSize", ScreenSize = new(this));
    }
}

public class UIInstanceBuffer : DynamicMesh
{
    private readonly Attribute<Vector2>
        attrPos,
        attrSize,
        attrUVPos,
        attrUVSize;
    private readonly Attribute<float> attrTexWeight;
    private readonly Attribute<IColor> attrColor;

    public UIInstanceBuffer(ITagContainer diContainer, bool dynamic = true) : base(diContainer, dynamic)
    {
        attrPos = AddAttribute<Vector2>("inPos");
        attrSize = AddAttribute<Vector2>("inSize");
        attrUVPos = AddAttribute<Vector2>("inUVPos");
        attrUVSize = AddAttribute<Vector2>("inUVSize");
        attrColor = AddAttribute<IColor>("inColor");
        attrTexWeight = AddAttribute<float>("inTexWeight");
    }

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
