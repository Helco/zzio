using System.Numerics;
using System.Runtime.InteropServices;
using Veldrid;
using zzio;
using zzre.rendering;

namespace zzre.materials;

[StructLayout(LayoutKind.Sequential)]
public struct ModelFactors
{
    public float vertexColorFactor;
    public float tintFactor;
    public float alphaReference;

    public static readonly ModelFactors Default = new()
    {
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
    public UniformBinding<FColor> Tint { get; }
    public UniformBinding<ModelFactors> Factors { get; }
    public TextureBinding Texture { get; }
    public SamplerBinding Sampler { get; }
    public SkeletonPoseBinding Pose { get; }

    public ModelMaterial(ITagContainer diContainer) : base(diContainer, "model")
    {
        AddBinding("world", World = new(this));
        AddBinding("projection", Projection = new(this));
        AddBinding("view", View = new(this));
        AddBinding("inTexShift", TexShift = new(this));
        AddBinding("inTint", Tint = new(this));
        AddBinding("factors", Factors = new(this));
        AddBinding("mainTexture", Texture = new(this));
        AddBinding("mainSampler", Sampler = new(this));
        AddBinding("pose", Pose = new(this));
    }
}

public class ModelInstanceBuffer : DynamicMesh
{
    private readonly Attribute<Matrix4x4> attrWorld;
    private readonly Attribute<Matrix3x2> attrTexShift;
    private readonly Attribute<IColor> attrTint;

    public ModelInstanceBuffer(ITagContainer diContainer, bool dynamic = true) : base(diContainer, dynamic)
    {
        attrWorld = AddAttribute<Matrix4x4>("world");
        attrTexShift = AddAttribute<Matrix3x2>("inTexShift");
        attrTint = AddAttribute<IColor>("inTint");
    }

    public void Add(ModelInstance i)
    {
        var index = Add(1);
        attrWorld[index] = i.world;
        attrTexShift[index] = i.texShift;
        attrTint[index] = i.tint;
    }
}

