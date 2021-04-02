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
    public struct SparkVertex
    {
        public Vector2 pos;
        public Vector2 tex;
        public const uint Stride =
            (2 + 2) * sizeof(float);
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SparkInstance
    {
        public Vector3 center;
        public Vector3 dir;
        public Vector4 color;
        public const uint Stride =
            (3 + 3 + 4) * sizeof(float);
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SparkUniforms
    {
        public FColor tint;
        public float tintFactor;
        public float instanceColorFactor;
        public float alphaReference;
        public const uint Stride = (4 + 3) * sizeof(float);

        public static readonly SparkUniforms Default = new SparkUniforms
        {
            tint = FColor.White,
            tintFactor = 1f,
            instanceColorFactor = 1f,
            alphaReference = 0.03f
        };
    }

    public abstract class SparkMaterial : BaseMaterial, IStandardTransformMaterial
    {
        public static SparkMaterial CreateFor(EffectPartRenderMode mode, bool isTwoSided, ITagContainer diContainer) => mode switch
        {
            EffectPartRenderMode.NormalBlend => new SparkBlendMaterial(diContainer, isTwoSided),
            EffectPartRenderMode.Additive => new SparkAdditiveMaterial(diContainer, isTwoSided),
            EffectPartRenderMode.AdditiveAlpha => new SparkAdditiveAlphaMaterial(diContainer, isTwoSided),
            _ => throw new NotSupportedException($"Unsupported effect part render mode {mode}")
        };

        public TextureBinding MainTexture { get; }
        public SamplerBinding Sampler { get; }
        public UniformBinding<Matrix4x4> Projection { get; }
        public UniformBinding<Matrix4x4> View { get; }
        public UniformBinding<Matrix4x4> World { get; }
        public UniformBinding<SparkUniforms> Uniforms { get; }

        public SparkMaterial(ITagContainer diContainer, IBuiltPipeline pipeline) : base(diContainer.GetTag<GraphicsDevice>(), pipeline)
        {
            Configure()
                .Add(MainTexture = new TextureBinding(this))
                .Add(Sampler = new SamplerBinding(this))
                .Add(Projection = new UniformBinding<Matrix4x4>(this))
                .Add(View = new UniformBinding<Matrix4x4>(this))
                .Add(World = new UniformBinding<Matrix4x4>(this))
                .Add(Uniforms = new UniformBinding<SparkUniforms>(this))
                .NextBindingSet();
        }

        protected static IPipelineBuilder BuildBasePipeline(IPipelineBuilder builder, bool isTwoSided) => builder
            .With(isTwoSided ? FaceCullMode.None : FaceCullMode.Back)
            .WithDepthWrite(false)
            .WithDepthTarget(PixelFormat.D24_UNorm_S8_UInt)
            .WithColorTarget(PixelFormat.R8_G8_B8_A8_UNorm)
            .WithShaderSet("Spark")
            .With("Position", VertexElementFormat.Float2, VertexElementSemantic.Position)
            .With("TexCoords", VertexElementFormat.Float2, VertexElementSemantic.TextureCoordinate)
            .NextVertexLayout()
            .WithInstanceStepRate(1)
            .With("Center", VertexElementFormat.Float3, VertexElementSemantic.Position)
            .With("Direction", VertexElementFormat.Float3, VertexElementSemantic.TextureCoordinate)
            .With("Color", VertexElementFormat.Float4, VertexElementSemantic.Color)
            .With("Texture", ResourceKind.TextureReadOnly, ShaderStages.Fragment)
            .With("Sampler", ResourceKind.Sampler, ShaderStages.Fragment)
            .With("Projection", ResourceKind.UniformBuffer, ShaderStages.Vertex)
            .With("View", ResourceKind.UniformBuffer, ShaderStages.Vertex)
            .With("World", ResourceKind.UniformBuffer, ShaderStages.Vertex)
            .With("MaterialBuffer", ResourceKind.UniformBuffer, ShaderStages.Fragment)
            .With(FrontFace.CounterClockwise);
    }

    public class SparkBlendMaterial : SparkMaterial
    {
        public SparkBlendMaterial(ITagContainer diContainer, bool isTwoSided) : base(diContainer, GetPipeline(diContainer, isTwoSided)) { }

        private static IBuiltPipeline GetPipeline(ITagContainer diContainer, bool isTwoSided) => PipelineFor<SparkBlendMaterial>.Get(diContainer, builder =>
            BuildBasePipeline(builder, isTwoSided)
            .With(BlendStateDescription.SingleAlphaBlend)
            .Build());
    }
    

    public class SparkAdditiveMaterial : SparkMaterial
    {
        public SparkAdditiveMaterial(ITagContainer diContainer, bool isTwoSided) : base(diContainer, GetPipeline(diContainer, isTwoSided)) { }

        private static IBuiltPipeline GetPipeline(ITagContainer diContainer, bool isTwoSided) => PipelineFor<SparkAdditiveMaterial>.Get(diContainer, builder =>
            BuildBasePipeline(builder, isTwoSided)
            .With(BlendStateDescription.SingleAdditiveBlend)
            .Build());
    }

    public class SparkAdditiveAlphaMaterial : SparkMaterial
    {
        public SparkAdditiveAlphaMaterial(ITagContainer diContainer, bool isTwoSided) : base(diContainer, GetPipeline(diContainer, isTwoSided))
        {}

        private static IBuiltPipeline GetPipeline(ITagContainer diContainer, bool isTwoSided) => PipelineFor<SparkAdditiveAlphaMaterial>.Get(diContainer, builder =>
            BuildBasePipeline(builder, isTwoSided)
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
