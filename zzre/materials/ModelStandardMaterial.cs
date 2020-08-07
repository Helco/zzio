using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using Veldrid;
using zzio.primitives;
using zzre.rendering;

namespace zzre.materials
{
    [StructLayout(LayoutKind.Sequential)]
    public struct ModelStandardVertex
    {
        public Vector3 pos;
        public Vector2 tex;
        public IColor color;
        public static uint Stride =
            (3 + 2) * sizeof(float) +
            4 * sizeof(byte);
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ModelStandardTransformationUniforms
    {
        public Matrix4x4 projection;
        public Matrix4x4 view;
        public Matrix4x4 world;
        public static uint Stride = (3 * 4 * 4) * sizeof(float);
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ModelStandardMaterialUniforms
    {
        public FColor tint;
        public float vertexColorFactor;
        public float tintFactor;
        public float alphaReference;
        public static uint Stride = (4 + 3) * sizeof(float);

        public static readonly ModelStandardMaterialUniforms Default = new ModelStandardMaterialUniforms
        {
            tint = FColor.White,
            vertexColorFactor = 1f,
            tintFactor = 1f,
            alphaReference = 0.03f
        };
    }

    public class ModelStandardMaterial : BaseMaterial
    {
        public TextureBinding MainTexture { get; }
        public SamplerBinding Sampler { get; }
        public UniformBinding<ModelStandardTransformationUniforms> Transformation { get; }
        public UniformBinding<ModelStandardMaterialUniforms> Uniforms { get; }

        public ModelStandardMaterial(ITagContainer diContainer) : base(diContainer.GetTag<GraphicsDevice>(), GetPipeline(diContainer))
        {
            Configure()
                .Add(MainTexture = new TextureBinding(this))
                .Add(Sampler = new SamplerBinding(this))
                .Add(Transformation = new UniformBinding<ModelStandardTransformationUniforms>(this))
                .Add(Uniforms = new UniformBinding<ModelStandardMaterialUniforms>(this))
                .NextBindingSet();
        }

        private static IBuiltPipeline GetPipeline(ITagContainer diContainer) => PipelineFor<ModelStandardMaterial>.Get(diContainer, builder => builder
            .WithDepthTarget(PixelFormat.D24_UNorm_S8_UInt)
            .WithColorTarget(PixelFormat.R8_G8_B8_A8_UNorm)
            .WithShaderSet("ModelStandard")
            .With("Position", VertexElementFormat.Float3, VertexElementSemantic.Position)
            .With("TexCoords", VertexElementFormat.Float2, VertexElementSemantic.TextureCoordinate)
            .With("Color", VertexElementFormat.Byte4_Norm, VertexElementSemantic.Color)
            .With("Texture", ResourceKind.TextureReadOnly, ShaderStages.Fragment)
            .With("Sampler", ResourceKind.Sampler, ShaderStages.Fragment)
            .With("TransformationBuffer", ResourceKind.UniformBuffer, ShaderStages.Vertex)
            .With("MaterialBuffer", ResourceKind.UniformBuffer, ShaderStages.Fragment)
            .With(FrontFace.CounterClockwise)
            .Build());
    }
}
