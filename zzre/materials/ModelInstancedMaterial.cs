using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Veldrid;
using zzio.effect;
using zzio.primitives;
using zzre.rendering;

namespace zzre.materials
{
    [StructLayout(LayoutKind.Sequential)]
    public struct ModelInstance
    {
        public Matrix4x4 world;
        public FColor tint;
        public static uint Stride = (4 * 4 + 4) * sizeof(float);
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ModelInstancedUniforms
    {
        public float vertexColorFactor;
        public float tintFactor;
        public float alphaReference;
        public static uint Stride = 3 * sizeof(float);

        public static readonly ModelInstancedUniforms Default = new ModelInstancedUniforms
        {
            vertexColorFactor = 1f,
            tintFactor = 1f,
            alphaReference = 0.6f
        };
    }

    public class BaseModelInstancedMaterial : BaseMaterial
    {
        public static BaseModelInstancedMaterial CreateFor(EffectPartRenderMode mode, ITagContainer diContainer) => mode switch
        {
            EffectPartRenderMode.NormalBlend => new ModelInstancedBlendMaterial(diContainer),
            EffectPartRenderMode.Additive => new ModelInstancedAdditiveMaterial(diContainer),
            EffectPartRenderMode.AdditiveAlpha => new ModelInstancedAdditiveAlphaMaterial(diContainer),
            _ => throw new NotSupportedException($"Unsupported effect part render mode {mode}")
        };

        public TextureBinding MainTexture { get; }
        public SamplerBinding Sampler { get; }
        public UniformBinding<Matrix4x4> Projection { get; }
        public UniformBinding<Matrix4x4> View { get; }
        public UniformBinding<ModelInstancedUniforms> Uniforms { get; }

        public BaseModelInstancedMaterial(ITagContainer diContainer, IBuiltPipeline pipeline) : base(diContainer.GetTag<GraphicsDevice>(), pipeline)
        {
            Configure()
                .Add(MainTexture = new TextureBinding(this))
                .Add(Sampler = new SamplerBinding(this))
                .Add(Projection = new UniformBinding<Matrix4x4>(this))
                .Add(View = new UniformBinding<Matrix4x4>(this))
                .Add(Uniforms = new UniformBinding<ModelInstancedUniforms>(this))
                .NextBindingSet();
        }

        protected static IPipelineBuilder BuildBasePipeline(IPipelineBuilder builder) => builder
            .WithDepthTarget(PixelFormat.D24_UNorm_S8_UInt)
            .WithColorTarget(PixelFormat.R8_G8_B8_A8_UNorm)
            .WithShaderSet("ModelInstanced")
            .With("Position", VertexElementFormat.Float3, VertexElementSemantic.Position)
            .With("TexCoords", VertexElementFormat.Float2, VertexElementSemantic.TextureCoordinate)
            .With("Color", VertexElementFormat.Byte4_Norm, VertexElementSemantic.Color)
            .NextVertexLayout()
            .WithInstanceStepRate(1)
            .With("World0", VertexElementFormat.Float4, VertexElementSemantic.TextureCoordinate)
            .With("World1", VertexElementFormat.Float4, VertexElementSemantic.TextureCoordinate)
            .With("World2", VertexElementFormat.Float4, VertexElementSemantic.TextureCoordinate)
            .With("World3", VertexElementFormat.Float4, VertexElementSemantic.TextureCoordinate)
            .With("Tint", VertexElementFormat.Float4, VertexElementSemantic.Color)
            .With("Texture", ResourceKind.TextureReadOnly, ShaderStages.Fragment)
            .With("Sampler", ResourceKind.Sampler, ShaderStages.Fragment)
            .With("Projection", ResourceKind.UniformBuffer, ShaderStages.Vertex)
            .With("View", ResourceKind.UniformBuffer, ShaderStages.Vertex)
            .With("MaterialBuffer", ResourceKind.UniformBuffer, ShaderStages.Fragment)
            .With(FrontFace.CounterClockwise);
    }

    public class ModelInstancedMaterial : BaseModelInstancedMaterial
    {
        public ModelInstancedMaterial(ITagContainer diContainer) : base(diContainer, GetPipeline(diContainer)) { }

        private static IBuiltPipeline GetPipeline(ITagContainer diContainer) => PipelineFor<ModelInstancedMaterial>.Get(diContainer, builder =>
            BuildBasePipeline(builder)
            .Build());
    }

    public class ModelInstancedBlendMaterial : BaseModelInstancedMaterial
    {
        public ModelInstancedBlendMaterial(ITagContainer diContainer) : base(diContainer, GetPipeline(diContainer)) { }

        private static IBuiltPipeline GetPipeline(ITagContainer diContainer) => PipelineFor<ModelInstancedMaterial>.Get(diContainer, builder =>
            BuildBasePipeline(builder)
            .WithDepthWrite(false)
            .With(BlendStateDescription.SingleAlphaBlend)
            .Build());
    }

    public class ModelInstancedAdditiveMaterial : BaseModelInstancedMaterial
    {
        public ModelInstancedAdditiveMaterial(ITagContainer diContainer) : base(diContainer, GetPipeline(diContainer)) { }

        private static IBuiltPipeline GetPipeline(ITagContainer diContainer) => PipelineFor<ModelInstancedMaterial>.Get(diContainer, builder =>
            BuildBasePipeline(builder)
            .WithDepthWrite(false)
            .With(BlendStateDescription.SingleAdditiveBlend)
            .Build());
    }

    public class ModelInstancedAdditiveAlphaMaterial : BaseModelInstancedMaterial
    {
        public ModelInstancedAdditiveAlphaMaterial(ITagContainer diContainer) : base(diContainer, GetPipeline(diContainer)) { }

        private static IBuiltPipeline GetPipeline(ITagContainer diContainer) => PipelineFor<ModelInstancedMaterial>.Get(diContainer, builder =>
            BuildBasePipeline(builder)
            .WithDepthWrite(false)
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
