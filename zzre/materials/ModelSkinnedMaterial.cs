using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using Veldrid;
using zzio.primitives;
using zzre.rendering;

namespace zzre.materials
{
    public class ModelSkinnedMaterial : BaseMaterial
    {
        public TextureBinding MainTexture { get; }
        public SamplerBinding Sampler { get; }
        public UniformBinding<TransformUniforms> Transformation { get; }
        public UniformBinding<ModelStandardMaterialUniforms> Uniforms { get; }
        public SkeletonPoseBinding Pose { get; }

        public ModelSkinnedMaterial(ITagContainer diContainer) : base(diContainer.GetTag<GraphicsDevice>(), GetPipeline(diContainer))
        {
            Configure()
                .Add(MainTexture = new TextureBinding(this))
                .Add(Sampler = new SamplerBinding(this))
                .Add(Transformation = new UniformBinding<TransformUniforms>(this))
                .Add(Uniforms = new UniformBinding<ModelStandardMaterialUniforms>(this))
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
            .With("TransformationBuffer", ResourceKind.UniformBuffer, ShaderStages.Vertex)
            .With("MaterialBuffer", ResourceKind.UniformBuffer, ShaderStages.Fragment)
            .With("PoseBuffer", ResourceKind.StructuredBufferReadOnly, ShaderStages.Vertex)
            .With(FrontFace.CounterClockwise)
            .Build());
    }
}
