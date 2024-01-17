using System.Numerics;
using System.Runtime.InteropServices;
using Veldrid;
using zzio;
using zzre.rendering;

namespace zzre.materials;

[StructLayout(LayoutKind.Sequential)]
public struct ModelColors
{
    public FColor tint;
    public float vertexColorFactor;
    public float tintFactor;
    public float alphaReference;
    public static uint Stride = (4 + 3) * sizeof(float);

    public static readonly ModelColors Default = new()
    {
        tint = FColor.White,
        vertexColorFactor = 1f,
        tintFactor = 1f,
        alphaReference = 0.6f
    };
}

public struct ModelInstance
{
    public Matrix4x4 world;
    public Matrix3x2 texShift;
    public IColor tint;
    public float vertexColorFactor;
    public float tintFactor;
    public float alphaReference;
}

public class ModelMaterial : MlangMaterial, IStandardTransformMaterial
{
    public enum BlendMode : uint
    {
        Opaque = 0,
        Alpha,
        Additive,
        AdditiveAlpha
    }

    public bool IsInstanced { set => SetOption(nameof(IsInstanced), value); }
    public bool IsSkinned { set => SetOption(nameof(IsSkinned), value); }
    public bool HasTexShift { set => SetOption(nameof(HasTexShift), value); }
    public bool HasEnvMap { set => SetOption(nameof(HasEnvMap), value); }
    public BlendMode Blend { set => SetOption(nameof(Blend), (uint)value); }

    public UniformBinding<Matrix4x4> World { get; }
    public UniformBinding<Matrix4x4> Projection { get; }
    public UniformBinding<Matrix4x4> View { get; }
    public UniformBinding<Matrix3x2> TexShift { get; }
    public UniformBinding<ModelColors> Colors { get; }
    public TextureBinding Texture { get; }
    public SamplerBinding Sampler { get; }
    public SkeletonPoseBinding Pose { get; }

    public ModelMaterial(ITagContainer diContainer) : base(diContainer, "model")
    {
        AddBinding("world", World = new(this));
        AddBinding("projection", Projection = new(this));
        AddBinding("view", View = new(this));
        AddBinding("inTexShift", TexShift = new(this));
        AddBinding("colors", Colors = new(this));
        AddBinding("mainTexture", Texture = new(this));
        AddBinding("mainSampler", Sampler = new(this));
        AddBinding("pose", Pose = new(this));
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

    public void Add(ModelInstance i)
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

