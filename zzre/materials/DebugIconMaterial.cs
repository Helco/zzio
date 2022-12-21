using System.Numerics;
using Veldrid;
using zzio;
using zzre.rendering;

namespace zzre.materials;

public struct DebugIconUniforms
{
    public Vector2 screenSize;
    public const uint Stride = 2 * sizeof(float);
}

public struct DebugIcon
{
    public Vector3 pos;
    public Vector2 uvCenter;
    public Vector2 uvSize;
    public float size;
    public IColor color;
    public const uint Stride =
        (3 + 2 + 2 + 1) * sizeof(float) +
        4 * sizeof(byte);
}

public class DebugIconMaterial : BaseMaterial, IStandardTransformMaterial
{
    public TextureBinding Texture { get; }
    public SamplerBinding Sampler { get; }
    public UniformBinding<Matrix4x4> Projection { get; }
    public UniformBinding<Matrix4x4> View { get; }
    public UniformBinding<Matrix4x4> World { get; }
    public UniformBinding<DebugIconUniforms> Uniforms { get; }

    public DebugIconMaterial(ITagContainer diContainer) : base(diContainer.GetTag<GraphicsDevice>(), GetPipeline(diContainer))
    {
        Configure()
            .Add(Texture = new TextureBinding(this))
            .Add(Sampler = new SamplerBinding(this))
            .Add(Projection = new UniformBinding<Matrix4x4>(this))
            .Add(View = new UniformBinding<Matrix4x4>(this))
            .Add(World = new UniformBinding<Matrix4x4>(this))
            .Add(Uniforms = new UniformBinding<DebugIconUniforms>(this))
            .NextBindingSet();
    }

    private static IBuiltPipeline GetPipeline(ITagContainer diContainer) => PipelineFor<DebugIconMaterial>.Get(diContainer, builder => builder
        .WithDepthTarget(PixelFormat.D24_UNorm_S8_UInt)
        .WithColorTarget(PixelFormat.R8_G8_B8_A8_UNorm)
        .WithShaderSet("DebugIcon")
        .WithInstanceStepRate(1)
        .With("Position", VertexElementFormat.Float3, VertexElementSemantic.Position)
        .With("UVCenter", VertexElementFormat.Float2, VertexElementSemantic.TextureCoordinate)
        .With("UVSize", VertexElementFormat.Float2, VertexElementSemantic.TextureCoordinate)
        .With("Size", VertexElementFormat.Float1, VertexElementSemantic.TextureCoordinate)
        .With("Color", VertexElementFormat.Byte4_Norm, VertexElementSemantic.Color)
        .With("Texture", ResourceKind.TextureReadOnly, ShaderStages.Fragment)
        .With("Sampler", ResourceKind.Sampler, ShaderStages.Fragment)
        .With("Projection", ResourceKind.UniformBuffer, ShaderStages.Vertex)
        .With("View", ResourceKind.UniformBuffer, ShaderStages.Vertex)
        .With("World", ResourceKind.UniformBuffer, ShaderStages.Vertex)
        .With("Uniforms", ResourceKind.UniformBuffer, ShaderStages.Vertex)
        .With(BlendStateDescription.SingleAlphaBlend)
        .With(PrimitiveTopology.TriangleStrip)
        .WithDepthWrite(false)
        .WithDepthTest(true)
        .With(FaceCullMode.None)
        .Build());
}
