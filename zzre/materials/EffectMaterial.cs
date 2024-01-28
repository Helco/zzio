using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Veldrid;
using zzio;
using zzio.effect;
using zzre.rendering;

namespace zzre.materials;

[StructLayout(LayoutKind.Sequential)]
public struct EffectFactors
{
    public float alphaReference;

    public static readonly EffectFactors Default = new()
    {
        alphaReference = 0.03f
    };
}

public class EffectMaterial : MlangMaterial, IStandardTransformMaterial
{
    public enum BillboardMode : uint
    {
        None,
        View,
        Spark
    }

    public enum BlendMode : uint
    {
        Additive,
        AdditiveAlpha,
        Alpha
    }

    public bool DepthTest { set => SetOption(nameof(DepthTest), value); }
    public BillboardMode Billboard { set => SetOption(nameof(Billboard), (uint)value); }
    public BlendMode Blend { set => SetOption(nameof(Blend), (uint)value); }

    public TextureBinding Texture { get; }
    public SamplerBinding Sampler { get; }
    public UniformBinding<Matrix4x4> Projection { get; }
    public UniformBinding<Matrix4x4> View { get; }
    public UniformBinding<Matrix4x4> World { get; }
    public UniformBinding<EffectFactors> Factors { get; }

    public EffectMaterial(ITagContainer diContainer) : base(diContainer, "effect")
    {
        DepthTest = true;
        AddBinding("mainTexture", Texture = new(this));
        AddBinding("mainSampler", Sampler = new(this));
        AddBinding("projection", Projection = new(this));
        AddBinding("view", View = new(this));
        AddBinding("world", World = new(this));
        AddBinding("factors", Factors = new(this));
    }
}

public class EffectMesh : DynamicMesh
{
    public Attribute<Vector3> Pos { get; }
    public Attribute<Vector2> UV { get; }
    public Attribute<IColor> Color { get; }
    public Attribute<Vector3> Center { get; }
    public Attribute<Vector3> Direction { get; }

    public EffectMesh(ITagContainer diContainer) : base(diContainer, dynamic: true)
    {
        Pos = AddAttribute<Vector3>("inVertexPos");
        UV = AddAttribute<Vector2>("inUV");
        Color = AddAttribute<IColor>("inColor");
        Center = AddAttribute<Vector3>("inCenterPos");
        Direction = AddAttribute<Vector3>("inDirection");
    }
}

[StructLayout(LayoutKind.Sequential)]
public struct EffectVertex
{
    public Vector3 pos;
    public Vector3 center;
    public Vector2 tex;
    public Vector4 color;
    public const uint Stride =
        (3 + 3 + 2 + 4) * sizeof(float);
}

[StructLayout(LayoutKind.Sequential)]
public struct EffectMaterialUniforms
{
    public FColor tint;
    public float vertexColorFactor;
    public float tintFactor;
    public float alphaReference;
    public bool isBillboard;
    public const uint Stride = (4 + 3) * sizeof(float) + sizeof(bool);

    public static readonly EffectMaterialUniforms Default = new()
    {
        tint = FColor.White,
        vertexColorFactor = 1f,
        tintFactor = 1f,
        alphaReference = 0.03f,
        isBillboard = true
    };
}


public abstract class EffectMaterialLEGACY : BaseMaterial, IStandardTransformMaterial
{
    public static EffectMaterialLEGACY CreateFor(EffectPartRenderMode mode, ITagContainer diContainer) => mode switch
    {
        EffectPartRenderMode.NormalBlend => new EffectBlendMaterial(diContainer),
        EffectPartRenderMode.Additive => new EffectAdditiveMaterial(diContainer),
        EffectPartRenderMode.AdditiveAlpha => new EffectAdditiveAlphaMaterial(diContainer),
        _ => throw new NotSupportedException($"Unsupported effect part render mode {mode}")
    };

    public TextureBinding MainTexture { get; }
    public SamplerBinding Sampler { get; }
    public UniformBinding<Matrix4x4> Projection { get; }
    public UniformBinding<Matrix4x4> View { get; }
    public UniformBinding<Matrix4x4> World { get; }
    public UniformBinding<EffectMaterialUniforms> Uniforms { get; }

    protected EffectMaterialLEGACY(ITagContainer diContainer, IBuiltPipeline pipeline) : base(diContainer.GetTag<GraphicsDevice>(), pipeline)
    {
        Configure()
            .Add(MainTexture = new TextureBinding(this))
            .Add(Sampler = new SamplerBinding(this))
            .Add(Projection = new UniformBinding<Matrix4x4>(this))
            .Add(View = new UniformBinding<Matrix4x4>(this))
            .Add(World = new UniformBinding<Matrix4x4>(this))
            .Add(Uniforms = new UniformBinding<EffectMaterialUniforms>(this))
            .NextBindingSet();
    }

    protected static IPipelineBuilder BuildBasePipeline(IPipelineBuilder builder) => builder
        .WithDepthWrite(false)
        .WithDepthTarget(PixelFormat.D24_UNorm_S8_UInt)
        .WithColorTarget(PixelFormat.R8_G8_B8_A8_UNorm)
        .WithShaderSet("Effect")
        .With("Position", VertexElementFormat.Float3, VertexElementSemantic.Position)
        .With("Center", VertexElementFormat.Float3, VertexElementSemantic.Position)
        .With("TexCoords", VertexElementFormat.Float2, VertexElementSemantic.TextureCoordinate)
        .With("Color", VertexElementFormat.Float4, VertexElementSemantic.Color)
        .With("Texture", ResourceKind.TextureReadOnly, ShaderStages.Fragment)
        .With("Sampler", ResourceKind.Sampler, ShaderStages.Fragment)
        .With("Projection", ResourceKind.UniformBuffer, ShaderStages.Vertex)
        .With("View", ResourceKind.UniformBuffer, ShaderStages.Vertex)
        .With("World", ResourceKind.UniformBuffer, ShaderStages.Vertex)
        .With("MaterialBuffer", ResourceKind.UniformBuffer, ShaderStages.Vertex | ShaderStages.Fragment)
        .With(FrontFace.CounterClockwise);
}

public class EffectBlendMaterial : EffectMaterialLEGACY
{
    public EffectBlendMaterial(ITagContainer diContainer) : base(diContainer, GetPipeline(diContainer)) { }

    private static IBuiltPipeline GetPipeline(ITagContainer diContainer) => PipelineFor<EffectBlendMaterial>.Get(diContainer, builder =>
        BuildBasePipeline(builder)
        .With(BlendStateDescription.SingleAlphaBlend)
        .Build());
}


public class EffectAdditiveMaterial : EffectMaterialLEGACY
{
    public EffectAdditiveMaterial(ITagContainer diContainer) : base(diContainer, GetPipeline(diContainer)) { }

    private static IBuiltPipeline GetPipeline(ITagContainer diContainer) => PipelineFor<EffectAdditiveMaterial>.Get(diContainer, builder =>
        BuildBasePipeline(builder)
        .With(BlendStateDescription.SingleAdditiveBlend)
        .Build());
}

public class EffectAdditiveAlphaMaterial : EffectMaterialLEGACY
{
    public EffectAdditiveAlphaMaterial(ITagContainer diContainer) : base(diContainer, GetPipeline(diContainer))
    { }

    private static IBuiltPipeline GetPipeline(ITagContainer diContainer) => PipelineFor<EffectAdditiveAlphaMaterial>.Get(diContainer, builder =>
        BuildBasePipeline(builder)
        .With(new BlendStateDescription(RgbaFloat.White,
            new BlendAttachmentDescription(true,
                sourceColorFactor: BlendFactor.SourceAlpha,
                sourceAlphaFactor: BlendFactor.SourceAlpha,
                destinationColorFactor: BlendFactor.One,
                destinationAlphaFactor: BlendFactor.One,
                colorFunction: BlendFunction.Add,
                alphaFunction: BlendFunction.Add)))
        .Build());
}
