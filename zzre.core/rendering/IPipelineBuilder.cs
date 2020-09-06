using System;
using System.Collections.Generic;
using System.Text;
using Veldrid;

namespace zzre.rendering
{
    public interface IBuiltPipeline
    {
        Pipeline Pipeline { get; }
        IReadOnlyList<ResourceLayout> ResourceLayouts { get; }
    }

    public interface IPipelineBuilder
    {
        IPipelineBuilder WithShaderSet(string shaderSetName);
        IPipelineBuilder With(OutputDescription outputDescr);
        IPipelineBuilder WithDepthTarget(PixelFormat format);
        IPipelineBuilder WithColorTarget(PixelFormat format);
        IPipelineBuilder With(TextureSampleCount sampleCount);
        IPipelineBuilder WithInstanceStepRate(uint instanceStepRate);
        IPipelineBuilder With(VertexElementDescription vertexElementDescr);
        IPipelineBuilder With(ResourceLayoutElementDescription resourceLayoutDescr);
        IPipelineBuilder With(RasterizerStateDescription rasterizerStateDescr);
        IPipelineBuilder With(FaceCullMode faceCullMode);
        IPipelineBuilder With(PolygonFillMode polygonFillMode);
        IPipelineBuilder With(FrontFace frontFace);
        IPipelineBuilder WithDepthClip(bool enabled = true);
        IPipelineBuilder WithScissorTest(bool enabled = true);
        IPipelineBuilder WithDepthTest(bool enabled = true);
        IPipelineBuilder WithDepthWrite(bool enabled = true);
        IPipelineBuilder With(ComparisonKind depthComparison);
        IPipelineBuilder With(BlendStateDescription blendStateDescr);
        IPipelineBuilder With(DepthStencilStateDescription depthStencilStateDescr);
        IPipelineBuilder With(PrimitiveTopology primitiveTopology);
        IPipelineBuilder NextVertexLayout();
        IPipelineBuilder NextResourceLayout();

        IPipelineBuilder With(string name, VertexElementFormat format, VertexElementSemantic semantic) =>
            With(new VertexElementDescription(name, format, semantic));
        IPipelineBuilder With(string name, ResourceKind kind, ShaderStages stages) =>
            With(new ResourceLayoutElementDescription(name, kind, stages));

        IBuiltPipeline Build();
    }
}
