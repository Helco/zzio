﻿using System.Numerics;
using Veldrid;
using zzre.rendering;

namespace zzre.materials;


public class DebugSkinSingleMaterial : BaseMaterial, IStandardTransformMaterial
{
    public UniformBinding<Matrix4x4> Projection { get; }
    public UniformBinding<Matrix4x4> View { get; }
    public UniformBinding<Matrix4x4> World { get; }
    public UniformBinding<int> BoneIndex { get; }

    public DebugSkinSingleMaterial(ITagContainer diContainer) : base(diContainer.GetTag<GraphicsDevice>(), GetPipeline(diContainer))
    {
        Configure()
            .Add(Projection = new UniformBinding<Matrix4x4>(this))
            .Add(View = new UniformBinding<Matrix4x4>(this))
            .Add(World = new UniformBinding<Matrix4x4>(this))
            .Add(BoneIndex = new UniformBinding<int>(this))
            .NextBindingSet();
    }

    private static IBuiltPipeline GetPipeline(ITagContainer diContainer) => PipelineFor<DebugSkinSingleMaterial>.Get(diContainer, builder => builder
        .WithDepthTarget(PixelFormat.D24_UNorm_S8_UInt)
        .WithColorTarget(PixelFormat.R8_G8_B8_A8_UNorm)
        .WithShaderSet("DebugSkinSingle")
        .With("Position", VertexElementFormat.Float3, VertexElementSemantic.Position)
        .With("DUMMYDebugSkinSingle", VertexElementFormat.Float2, VertexElementSemantic.TextureCoordinate) // use one unique name because of Veldrid#294
        .With("Color", VertexElementFormat.Byte4_Norm, VertexElementSemantic.Color)
        .NextVertexLayout()
        .With("Weights", VertexElementFormat.Float4, VertexElementSemantic.TextureCoordinate)
        .With("Indices", VertexElementFormat.Byte4, VertexElementSemantic.TextureCoordinate)
        .With("Projection", ResourceKind.UniformBuffer, ShaderStages.Vertex)
        .With("View", ResourceKind.UniformBuffer, ShaderStages.Vertex)
        .With("World", ResourceKind.UniformBuffer, ShaderStages.Vertex)
        .With("BoneIndex", ResourceKind.UniformBuffer, ShaderStages.Vertex)
        .With(FrontFace.CounterClockwise)
        .With(BlendStateDescription.SingleAlphaBlend)
        .WithDepthTest(false)
        .WithDepthWrite(false)
        .Build());
}
