using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using Veldrid;
using zzio.primitives;
using zzre.rendering;

namespace zzre.materials
{
    public class DebugLinesMaterial : BaseMaterial, IStandardTransformMaterial
    {
        public UniformBinding<Matrix4x4> Projection { get; }
        public UniformBinding<Matrix4x4> View { get; }
        public UniformBinding<Matrix4x4> World { get; }

        public DebugLinesMaterial(ITagContainer diContainer) : base(diContainer.GetTag<GraphicsDevice>(), GetPipeline(diContainer))
        {
            Configure()
                .Add(Projection = new UniformBinding<Matrix4x4>(this))
                .Add(View = new UniformBinding<Matrix4x4>(this))
                .Add(World = new UniformBinding<Matrix4x4>(this))
                .NextBindingSet();
        }

        private static IBuiltPipeline GetPipeline(ITagContainer diContainer) => PipelineFor<DebugLinesMaterial>.Get(diContainer, builder => builder
            .WithDepthTarget(PixelFormat.D24_UNorm_S8_UInt)
            .WithColorTarget(PixelFormat.R8_G8_B8_A8_UNorm)
            .WithShaderSet("VertexColor")
            .With("Position", VertexElementFormat.Float3, VertexElementSemantic.Position)
            .With("Color", VertexElementFormat.Byte4_Norm, VertexElementSemantic.Color)
            .With("Projection", ResourceKind.UniformBuffer, ShaderStages.Vertex)
            .With("View", ResourceKind.UniformBuffer, ShaderStages.Vertex)
            .With("World", ResourceKind.UniformBuffer, ShaderStages.Vertex)
            .With(PrimitiveTopology.LineList)
            .WithDepthWrite(false)
            .WithDepthTest(false)
            .Build());
    }
}
