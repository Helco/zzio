using System;
using System.Numerics;
using Veldrid;
using zzre;
using zzre.materials;
using zzre.rendering;

namespace zzmaps;


public interface IMapMaterial : IStandardTransformMaterial, IDisposable
{
    public UniformBinding<ModelStandardMaterialUniforms> Uniforms { get; }
    public UniformBinding<uint> PixelCounter { get; }
}

public class MapStandardMaterial : BaseMaterial, IMapMaterial
{
    public TextureBinding MainTexture { get; }
    public SamplerBinding Sampler { get; }
    public UniformBinding<Matrix4x4> Projection { get; }
    public UniformBinding<Matrix4x4> View { get; }
    public UniformBinding<Matrix4x4> World { get; }
    public UniformBinding<ModelStandardMaterialUniforms> Uniforms { get; }
    public UniformBinding<uint> PixelCounter { get; }

    public MapStandardMaterial(ITagContainer diContainer) : base(diContainer.GetTag<GraphicsDevice>(), GetPipeline(diContainer))
    {
        Configure()
            .Add(MainTexture = new TextureBinding(this))
            .Add(Sampler = new SamplerBinding(this))
            .Add(Projection = new UniformBinding<Matrix4x4>(this))
            .Add(View = new UniformBinding<Matrix4x4>(this))
            .Add(World = new UniformBinding<Matrix4x4>(this))
            .Add(Uniforms = new UniformBinding<ModelStandardMaterialUniforms>(this))
            .Add(PixelCounter = new UniformBinding<uint>(this))
            .NextBindingSet();
    }

    private static IBuiltPipeline GetPipeline(ITagContainer diContainer) => PipelineFor<MapStandardMaterial>.Get(diContainer, builder => builder
        .WithDepthTarget(PixelFormat.D24_UNorm_S8_UInt)
        .WithColorTarget(PixelFormat.R8_G8_B8_A8_UNorm)
        .WithShaderSet("MapStandard")
        .With("Position", VertexElementFormat.Float3, VertexElementSemantic.Position)
        .With("TexCoords", VertexElementFormat.Float2, VertexElementSemantic.TextureCoordinate)
        .With("Color", VertexElementFormat.Byte4_Norm, VertexElementSemantic.Color)
        .With("Texture", ResourceKind.TextureReadOnly, ShaderStages.Fragment)
        .With("Sampler", ResourceKind.Sampler, ShaderStages.Fragment)
        .With("Projection", ResourceKind.UniformBuffer, ShaderStages.Vertex)
        .With("View", ResourceKind.UniformBuffer, ShaderStages.Vertex)
        .With("World", ResourceKind.UniformBuffer, ShaderStages.Vertex)
        .With("MaterialBuffer", ResourceKind.UniformBuffer, ShaderStages.Fragment)
        .With("PixelCounter", ResourceKind.StructuredBufferReadWrite, ShaderStages.Fragment)
        .With(FrontFace.CounterClockwise)
        .Build());
}
