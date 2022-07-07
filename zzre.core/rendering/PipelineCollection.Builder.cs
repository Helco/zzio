using System;
using System.Collections.Generic;
using System.Linq;
using Veldrid;
using zzio;

namespace zzre.rendering
{
    public partial class PipelineCollection
    {
        private class PipelineBuilder : IPipelineBuilder
        {
            private PipelineCollection Collection { get; }
            private string? shaderSetName;
            private PixelFormat? depthTarget;
            private List<PixelFormat> colorTargets = new List<PixelFormat>();
            private TextureSampleCount sampleCount = TextureSampleCount.Count1;
            private readonly List<List<VertexElementDescription>> vertexElements = new List<List<VertexElementDescription>>()
            {
                new List<VertexElementDescription>()
            };
            private readonly List<uint> vertexLayoutInstanceStepRates = new List<uint>()
            {
                0
            };
            private readonly List<List<ResourceLayoutElementDescription>> resLayoutElements = new List<List<ResourceLayoutElementDescription>>()
            {
                new List<ResourceLayoutElementDescription>()
            };
            private RasterizerStateDescription rasterizer = RasterizerStateDescription.Default;
            private BlendStateDescription blendState = BlendStateDescription.SingleOverrideBlend;
            private DepthStencilStateDescription depthStencil = DepthStencilStateDescription.DepthOnlyLessEqual;
            private PrimitiveTopology primitiveTopology = PrimitiveTopology.TriangleList;

            public PipelineBuilder(PipelineCollection collection)
            {
                Collection = collection;
            }

            public IPipelineBuilder WithShaderSet(string shaderSetName)
            {
                this.shaderSetName = shaderSetName;
                return this;
            }

            public IPipelineBuilder With(OutputDescription outputDescr)
            {
                depthTarget = outputDescr.DepthAttachment?.Format;
                colorTargets = outputDescr.ColorAttachments.Select(c => c.Format).ToList();
                sampleCount = outputDescr.SampleCount;
                return this;
            }

            public IPipelineBuilder WithDepthTarget(PixelFormat format)
            {
                if (depthTarget != null)
                    throw new InvalidOperationException("A depth target is already attached");
                depthTarget = format;
                return this;
            }

            public IPipelineBuilder WithColorTarget(PixelFormat format)
            {
                colorTargets.Add(format);
                return this;
            }

            public IPipelineBuilder With(TextureSampleCount sampleCount)
            {
                this.sampleCount = sampleCount;
                return this;
            }

            public IPipelineBuilder WithInstanceStepRate(uint instanceStepRate)
            {
                vertexLayoutInstanceStepRates[vertexLayoutInstanceStepRates.Count - 1] = instanceStepRate;
                return this;
            }

            public IPipelineBuilder With(VertexElementDescription vertexElementDescr)
            {
                if (Collection.Factory.BackendType == GraphicsBackend.Direct3D11)
                    vertexElementDescr.Semantic = VertexElementSemantic.TextureCoordinate; // TODO: Maybe watch the Veldrid bug for that?

                vertexElements.Last().Add(vertexElementDescr);
                return this;
            }

            public IPipelineBuilder With(ResourceLayoutElementDescription resourceLayoutDescr)
            {
                resLayoutElements.Last().Add(resourceLayoutDescr);
                return this;
            }

            public IPipelineBuilder With(RasterizerStateDescription rasterizerStateDescr)
            {
                rasterizer = rasterizerStateDescr;
                return this;
            }

            public IPipelineBuilder With(FaceCullMode faceCullMode)
            {
                rasterizer.CullMode = faceCullMode;
                return this;
            }

            public IPipelineBuilder With(PolygonFillMode polygonFillMode)
            {
                rasterizer.FillMode = polygonFillMode;
                return this;
            }

            public IPipelineBuilder With(FrontFace frontFace)
            {
                rasterizer.FrontFace = frontFace;
                return this;
            }

            public IPipelineBuilder WithDepthClip(bool enabled = true)
            {
                rasterizer.DepthClipEnabled = enabled;
                return this;
            }

            public IPipelineBuilder WithScissorTest(bool enabled = true)
            {
                rasterizer.ScissorTestEnabled = enabled;
                return this;
            }

            public IPipelineBuilder WithDepthTest(bool enabled = true)
            {
                depthStencil.DepthTestEnabled = enabled;
                return this;
            }

            public IPipelineBuilder WithDepthWrite(bool enabled = true)
            {
                depthStencil.DepthWriteEnabled = enabled;
                return this;
            }

            public IPipelineBuilder With(ComparisonKind kind)
            {
                depthStencil.DepthComparison = kind;
                return this;
            }

            public IPipelineBuilder With(BlendStateDescription blendStateDescr)
            {
                blendState = blendStateDescr;
                return this;
            }

            public IPipelineBuilder With(DepthStencilStateDescription depthStencilStateDescr)
            {
                depthStencil = depthStencilStateDescr;
                return this;
            }

            public IPipelineBuilder With(PrimitiveTopology primitiveTopology)
            {
                this.primitiveTopology = primitiveTopology;
                return this;
            }

            public IPipelineBuilder NextVertexLayout()
            {
                if (!vertexElements.Last().Any())
                    throw new InvalidOperationException("Last vertex layout has no elements");
                vertexElements.Add(new List<VertexElementDescription>());
                vertexLayoutInstanceStepRates.Add(0);
                return this;
            }

            public IPipelineBuilder NextResourceLayout()
            {
                if (!resLayoutElements.Last().Any())
                    throw new InvalidOperationException("Last resource layout has no elements");
                resLayoutElements.Add(new List<ResourceLayoutElementDescription>());
                return this;
            }

            public IBuiltPipeline Build()
            {
                if (shaderSetName == null)
                    throw new InvalidOperationException("No shader set was specified");
                if (depthTarget == null && colorTargets.Count == 0)
                    throw new InvalidOperationException("Neither a depth target nor a color target was specified");
                if (!vertexElements.Last().Any())
                    throw new InvalidOperationException("Last vertex layout has no elements");
                if (!resLayoutElements.Last().Any())
                    throw new InvalidOperationException("Last resource layout has no elements");

                lock (Collection)
                {
                    var builtPipeline = Collection.pipelines.FirstOrDefault(Equals);
                    if (builtPipeline != null)
                        return builtPipeline;

                    var vertexLayouts = vertexElements
                        .Select(set => new VertexLayoutDescription(set.ToArray()))
                        .Select((set, i) => { set.InstanceStepRate = vertexLayoutInstanceStepRates[i]; return set; })
                        .ToArray();
                    var resLayoutDescrs = resLayoutElements
                        .Select(set => new ResourceLayoutDescription(set.ToArray()))
                        .ToArray();
                    var resourceLayouts = resLayoutDescrs
                        .Select(layoutDescr => Collection.Factory.CreateResourceLayout(layoutDescr))
                        .ToArray();
                    var shaders = Collection.LoadShaderSet(shaderSetName);
                    var pipelineDescr = new GraphicsPipelineDescription(
                        blendState,
                        depthStencil,
                        rasterizer,
                        primitiveTopology,
                        new ShaderSetDescription(vertexLayouts, shaders),
                        resourceLayouts,
                        OutputDescription);
                    var pipeline = Collection.Factory.CreateGraphicsPipeline(pipelineDescr);
                    builtPipeline = new BuiltPipeline(pipeline, pipelineDescr, resLayoutDescrs, shaderSetName);

                    Collection.pipelines.Add(builtPipeline);
                    return builtPipeline;
                }
            }

            private OutputDescription OutputDescription => new OutputDescription(
                    depthTarget != null ? new OutputAttachmentDescription(depthTarget.Value) : null,
                    colorTargets.Select(f => new OutputAttachmentDescription(f)).ToArray(),
                    sampleCount);

            private bool Equals(BuiltPipeline built)
            {
                return
                    built.ShaderSetName == shaderSetName &&
                    built.Description.Outputs.Equals(OutputDescription) &&
                    AreVertexLayoutsEqual(built.Description.ShaderSet.VertexLayouts) &&
                    AreResourceLayoutsEqual(built.ResourceLayoutDescriptions) &&
                    built.Description.RasterizerState.Equals(rasterizer) &&
                    built.Description.BlendState.Equals(blendState) &&
                    built.Description.DepthStencilState.Equals(depthStencil) &&
                    built.Description.PrimitiveTopology == primitiveTopology;
            }

            private static bool Equals<T>(IEnumerable<T[]> aSetSet, List<List<T>> bSetSet) where T : struct
            {
                if (aSetSet.Count() != bSetSet.Count())
                    return false;
                foreach (var (aSet, setI) in aSetSet.Indexed())
                {
                    var bSet = bSetSet[setI];
                    if (aSet.Length != bSet.Count)
                        return false;
                    if (aSet.Any((element, elementI) => !element.Equals(bSet[elementI])))
                        return false;
                }
                return true;
            }

            private bool AreVertexLayoutsEqual(VertexLayoutDescription[] vertexLayouts) =>
                Equals(vertexLayouts.Select(l => l.Elements), vertexElements);

            private bool AreResourceLayoutsEqual(ResourceLayoutDescription[] resourceLayouts) =>
                Equals(resourceLayouts.Select(l => l.Elements), resLayoutElements);
        }
    }
}
