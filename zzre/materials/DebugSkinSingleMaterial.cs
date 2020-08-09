using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using Veldrid;
using zzio.primitives;
using zzre.rendering;

namespace zzre.materials
{

    public class DebugSkinSingleMaterial : BaseMaterial
    {
        public UniformBinding<TransformUniforms> Transformation { get; }
        public UniformBinding<int> BoneIndex { get; }

        public DebugSkinSingleMaterial(ITagContainer diContainer) : base(diContainer.GetTag<GraphicsDevice>(), GetPipeline(diContainer))
        {
            Configure()
                .Add(Transformation = new UniformBinding<TransformUniforms>(this))
                .Add(BoneIndex = new UniformBinding<int>(this))
                .NextBindingSet();
        }

        private static IBuiltPipeline GetPipeline(ITagContainer diContainer) => PipelineFor<DebugSkinSingleMaterial>.Get(diContainer, builder => builder
            .WithDepthTarget(PixelFormat.D24_UNorm_S8_UInt)
            .WithColorTarget(PixelFormat.R8_G8_B8_A8_UNorm)
            .WithShaderSet("DebugSkinSingle")
            .With("Position", VertexElementFormat.Float3, VertexElementSemantic.Position)
            .With("TexCoords", VertexElementFormat.Float2, VertexElementSemantic.TextureCoordinate)
            .With("Color", VertexElementFormat.Byte4_Norm, VertexElementSemantic.Color)
            .NextVertexLayout()
            .With("Weights", VertexElementFormat.Float4, VertexElementSemantic.TextureCoordinate)
            .With("Indices", VertexElementFormat.Byte4, VertexElementSemantic.TextureCoordinate)
            .With("TransformationBuffer", ResourceKind.UniformBuffer, ShaderStages.Vertex)
            .With("BoneIndex", ResourceKind.UniformBuffer, ShaderStages.Vertex)
            .With(FrontFace.CounterClockwise)
            .With(BlendStateDescription.SingleAlphaBlend)
            .WithDepthTest(false)
            .WithDepthWrite(false)
            .Build());
    }
}
