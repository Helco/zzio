using System.Numerics;
using Veldrid;
using zzre.rendering;

namespace zzre.materials;

public class ModelSkinnedMaterial : BaseMaterial, IStandardTransformMaterial
{
    public TextureBinding MainTexture { get; }
    public SamplerBinding Sampler { get; }
    public UniformBinding<Matrix4x4> Projection { get; }
    public UniformBinding<Matrix4x4> View { get; }
    public UniformBinding<Matrix4x4> World { get; }
    public UniformBinding<ModelColors> Uniforms { get; }
    public SkeletonPoseBinding Pose { get; }

    public ModelSkinnedMaterial(ITagContainer diContainer) : base(diContainer.GetTag<GraphicsDevice>(), GetPipeline(diContainer))
    {
        Configure()
            .Add(MainTexture = new TextureBinding(this))
            .Add(Sampler = new SamplerBinding(this))
            .Add(Projection = new UniformBinding<Matrix4x4>(this))
            .Add(View = new UniformBinding<Matrix4x4>(this))
            .Add(World = new UniformBinding<Matrix4x4>(this))
            .Add(Uniforms = new UniformBinding<ModelColors>(this))
            .Add(Pose = new SkeletonPoseBinding(this))
            .NextBindingSet();
    }

    private static IBuiltPipeline GetPipeline(ITagContainer diContainer) => PipelineFor<ModelSkinnedMaterial>.Get(diContainer, builder => builder
        .WithDepthTarget(PixelFormat.D24_UNorm_S8_UInt)
        .WithColorTarget(PixelFormat.R8_G8_B8_A8_UNorm)
        .WithShaderSet("ModelSkinned")
        .With("Position", VertexElementFormat.Float3, VertexElementSemantic.Position)
        .With("TexCoords", VertexElementFormat.Float2, VertexElementSemantic.TextureCoordinate)
        .With("Color", VertexElementFormat.Byte4_Norm, VertexElementSemantic.Color)
        .NextVertexLayout()
        .With("Weights", VertexElementFormat.Float4, VertexElementSemantic.TextureCoordinate)
        .With("Indices", VertexElementFormat.Byte4, VertexElementSemantic.TextureCoordinate)
        .With("Texture", ResourceKind.TextureReadOnly, ShaderStages.Fragment)
        .With("Sampler", ResourceKind.Sampler, ShaderStages.Fragment)
        .With("Projection", ResourceKind.UniformBuffer, ShaderStages.Vertex)
        .With("View", ResourceKind.UniformBuffer, ShaderStages.Vertex)
        .With("World", ResourceKind.UniformBuffer, ShaderStages.Vertex)
        .With("MaterialBuffer", ResourceKind.UniformBuffer, ShaderStages.Fragment)
        .With("PoseBuffer", ResourceKind.StructuredBufferReadOnly, ShaderStages.Vertex)
        .With(FrontFace.CounterClockwise)
        .Build());
}
