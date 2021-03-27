using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using Veldrid;
using zzio.effect;
using zzio.primitives;
using zzre.rendering;

namespace zzre.materials
{
    [StructLayout(LayoutKind.Sequential)]
    public struct EffectVertex
    {
        public Vector3 pos;
        public Vector2 tex;
        public Vector4 color;
        public const uint Stride =
            (3 + 2 + 4) * sizeof(float);
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct EffectMaterialUniforms
    {
        public FColor tint;
        public float vertexColorFactor;
        public float tintFactor;
        public float alphaReference;
        public const uint Stride = (4 + 3) * sizeof(float);

        public static readonly EffectMaterialUniforms Default = new EffectMaterialUniforms
        {
            tint = FColor.White,
            vertexColorFactor = 1f,
            tintFactor = 1f,
            alphaReference = 0.03f
        };
    }

    public abstract class EffectMaterial : BaseMaterial, IStandardTransformMaterial
    {
        public static EffectMaterial CreateFor(EffectPartRenderMode mode, ITagContainer diContainer) => mode switch
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

        public EffectMaterial(ITagContainer diContainer, IBuiltPipeline pipeline) : base(diContainer.GetTag<GraphicsDevice>(), pipeline)
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
            .WithDepthTarget(PixelFormat.D24_UNorm_S8_UInt)
            .WithColorTarget(PixelFormat.R8_G8_B8_A8_UNorm)
            .WithShaderSet("ModelStandard")
            .With("Position", VertexElementFormat.Float3, VertexElementSemantic.Position)
            .With("TexCoords", VertexElementFormat.Float2, VertexElementSemantic.TextureCoordinate)
            .With("Color", VertexElementFormat.Float4, VertexElementSemantic.Color)
            .With("Texture", ResourceKind.TextureReadOnly, ShaderStages.Fragment)
            .With("Sampler", ResourceKind.Sampler, ShaderStages.Fragment)
            .With("Projection", ResourceKind.UniformBuffer, ShaderStages.Vertex)
            .With("View", ResourceKind.UniformBuffer, ShaderStages.Vertex)
            .With("World", ResourceKind.UniformBuffer, ShaderStages.Vertex)
            .With("MaterialBuffer", ResourceKind.UniformBuffer, ShaderStages.Fragment)
            .With(FrontFace.CounterClockwise);
    }

    public class EffectBlendMaterial : EffectMaterial
    {
        public EffectBlendMaterial(ITagContainer diContainer) : base(diContainer, GetPipeline(diContainer)) { }

        private static IBuiltPipeline GetPipeline(ITagContainer diContainer) => PipelineFor<EffectBlendMaterial>.Get(diContainer, builder =>
            BuildBasePipeline(builder)
            .With(BlendStateDescription.SingleAlphaBlend)
            .Build());
    }
    

    public class EffectAdditiveMaterial : EffectMaterial
    {
        public EffectAdditiveMaterial(ITagContainer diContainer) : base(diContainer, GetPipeline(diContainer)) { }

        private static IBuiltPipeline GetPipeline(ITagContainer diContainer) => PipelineFor<EffectAdditiveMaterial>.Get(diContainer, builder =>
            BuildBasePipeline(builder)
            .With(BlendStateDescription.SingleAdditiveBlend)
            .Build());
    }

    public class EffectAdditiveAlphaMaterial : EffectMaterial
    {
        public EffectAdditiveAlphaMaterial(ITagContainer diContainer) : base(diContainer, GetPipeline(diContainer))
        {}

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
}
