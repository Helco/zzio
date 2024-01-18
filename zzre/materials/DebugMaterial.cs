using System.Numerics;
using Veldrid;
using zzio;
using zzre.rendering;

namespace zzre.materials;

public class DebugMaterial : MlangMaterial, IStandardTransformMaterial
{
    public enum ColorMode : uint
    {
        VertexColor = 0,
        SkinWeights,
        SingleBoneWeight
    }

    public enum TopologyMode : uint
    {
        Triangles = 0,
        Lines
    }

    public bool IsSkinned { set => SetOption(nameof(IsSkinned), value); }
    public ColorMode Color { set => SetOption(nameof(ColorMode), (uint)value); }
    public TopologyMode Topology { set => SetOption(nameof(Topology), (uint)value); }

    public UniformBinding<Matrix4x4> Projection { get; }
    public UniformBinding<Matrix4x4> View { get; }
    public UniformBinding<Matrix4x4> World { get; }
    public SkeletonPoseBinding Pose { get; }

    public DebugMaterial(ITagContainer diContainer) : base(diContainer, "debug")
    {
        AddBinding("world", World = new(this));
        AddBinding("projection", Projection = new(this));
        AddBinding("view", View = new(this));
        AddBinding("pose", Pose = new(this));
    }
}

public class DebugDynamicMesh : DynamicMesh
{
    private readonly Attribute<Vector3> attrPos;
    private readonly Attribute<IColor> attrColor;

    public DebugDynamicMesh(ITagContainer diContainer, bool dynamic = true) : base(diContainer, dynamic)
    {
        attrPos = AddAttribute<Vector3>("inPos");
        attrColor = AddAttribute<IColor>("inColor");
    }

    public void Add(ColoredVertex v)
    {
        var index = Add(1);
        attrPos[index] = v.pos;
        attrColor[index] = v.color;
    }
}

public class DebugLegacyMaterial : BaseMaterial, IStandardTransformMaterial
{
    public UniformBinding<Matrix4x4> Projection { get; }
    public UniformBinding<Matrix4x4> View { get; }
    public UniformBinding<Matrix4x4> World { get; }

    public DebugLegacyMaterial(ITagContainer diContainer) : base(diContainer.GetTag<GraphicsDevice>(), GetPipeline(diContainer))
    {
        Configure()
            .Add(Projection = new UniformBinding<Matrix4x4>(this))
            .Add(View = new UniformBinding<Matrix4x4>(this))
            .Add(World = new UniformBinding<Matrix4x4>(this))
            .NextBindingSet();
    }

    private static IBuiltPipeline GetPipeline(ITagContainer diContainer) => PipelineFor<DebugLegacyMaterial>.Get(diContainer, builder => builder
        .WithDepthTarget(PixelFormat.D24_UNorm_S8_UInt)
        .WithColorTarget(PixelFormat.R8_G8_B8_A8_UNorm)
        .WithShaderSet("VertexColor")
        .With("Position", VertexElementFormat.Float3, VertexElementSemantic.Position)
        .With("Color", VertexElementFormat.Byte4_Norm, VertexElementSemantic.Color)
        .With("Projection", ResourceKind.UniformBuffer, ShaderStages.Vertex)
        .With("View", ResourceKind.UniformBuffer, ShaderStages.Vertex)
        .With("World", ResourceKind.UniformBuffer, ShaderStages.Vertex)
        .With(BlendStateDescription.SingleAlphaBlend)
        .WithDepthWrite(false)
        .WithDepthTest(false)
        .With(FaceCullMode.None)
        .Build());
}
