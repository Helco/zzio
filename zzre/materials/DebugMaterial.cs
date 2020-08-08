using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using Veldrid;
using zzio.primitives;
using zzre.rendering;

namespace zzre.materials
{
    public class DebugMaterial : BaseMaterial
    {
        public UniformBinding<TransformUniforms> Transformation { get; }

        public DebugMaterial(ITagContainer diContainer) : base(diContainer.GetTag<GraphicsDevice>(), GetPipeline(diContainer))
        {
            Configure()
                .Add(Transformation = new UniformBinding<TransformUniforms>(this))
                .NextBindingSet();
        }

        private static IBuiltPipeline GetPipeline(ITagContainer diContainer) => PipelineFor<DebugMaterial>.Get(diContainer, builder => builder
            .WithDepthTarget(PixelFormat.D24_UNorm_S8_UInt)
            .WithColorTarget(PixelFormat.R8_G8_B8_A8_UNorm)
            .WithShaderSet("VertexColor")
            .With("Position", VertexElementFormat.Float3, VertexElementSemantic.Position)
            .With("Color", VertexElementFormat.Byte4_Norm, VertexElementSemantic.Color)
            .With("TransformationBuffer", ResourceKind.UniformBuffer, ShaderStages.Vertex)
            .With(BlendStateDescription.SingleAlphaBlend)
            .WithDepthWrite(false)
            .WithDepthTest(false)
            .Build());
    }
}
