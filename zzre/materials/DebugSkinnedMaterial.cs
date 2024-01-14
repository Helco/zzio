using System.Numerics;
using Veldrid;
using zzre.rendering;

namespace zzre.materials;

public class DebugSkinnedMaterial : BaseMaterial, IStandardTransformMaterial
{
    public UniformBinding<Matrix4x4> Projection { get; }
    public UniformBinding<Matrix4x4> View { get; }
    public UniformBinding<Matrix4x4> World { get; }
    public SkeletonPoseBinding Pose { get; }

    public DebugSkinnedMaterial(ITagContainer diContainer) : base(diContainer.GetTag<GraphicsDevice>(), GetPipeline(diContainer))
    {
        Configure()
            .Add(Projection = new UniformBinding<Matrix4x4>(this))
            .Add(View = new UniformBinding<Matrix4x4>(this))
            .Add(World = new UniformBinding<Matrix4x4>(this))
            .Add(Pose = new SkeletonPoseBinding(this))
            .NextBindingSet();
    }

    private static IBuiltPipeline GetPipeline(ITagContainer diContainer) => PipelineFor<DebugSkinnedMaterial>.Get(diContainer, builder => builder
        .WithDepthTarget(PixelFormat.D24_UNorm_S8_UInt)
        .WithColorTarget(PixelFormat.R8_G8_B8_A8_UNorm)
        .WithShaderSet("VertexColorSkinned")
        .With("Position", VertexElementFormat.Float3, VertexElementSemantic.Position)
        .With("Color", VertexElementFormat.Byte4_Norm, VertexElementSemantic.Color)
        .NextVertexLayout()
        .With("Weights", VertexElementFormat.Float4, VertexElementSemantic.TextureCoordinate)
        .With("Indices", VertexElementFormat.Byte4, VertexElementSemantic.TextureCoordinate)
        .With("Projection", ResourceKind.UniformBuffer, ShaderStages.Vertex)
        .With("View", ResourceKind.UniformBuffer, ShaderStages.Vertex)
        .With("World", ResourceKind.UniformBuffer, ShaderStages.Vertex)
        .With("PoseBuffer", ResourceKind.StructuredBufferReadOnly, ShaderStages.Vertex)
        .With(FrontFace.CounterClockwise)
        .With(BlendStateDescription.SingleAlphaBlend)
        .WithDepthWrite(false)
        .WithDepthTest(false)
        .Build());
}
