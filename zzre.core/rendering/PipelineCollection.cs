using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Veldrid;
using Veldrid.SPIRV;

namespace zzre.rendering
{
    public partial class PipelineCollection : BaseDisposable
    {
        private class BuiltPipeline : IBuiltPipeline
        {
            public Pipeline Pipeline { get; }
            public GraphicsPipelineDescription Description { get; }
            public ResourceLayoutDescription[] ResourceLayoutDescriptions { get; }
            public string ShaderSetName { get; }
            public IReadOnlyList<ResourceLayout> ResourceLayouts => Description.ResourceLayouts;

            public BuiltPipeline(Pipeline pipeline, GraphicsPipelineDescription descr, ResourceLayoutDescription[] resLayoutDescriptions, string shaderSetName)
            {
                Pipeline = pipeline;
                Description = descr;
                ResourceLayoutDescriptions = resLayoutDescriptions;
                ShaderSetName = shaderSetName;
            }
        }
        private List<BuiltPipeline> pipelines = new List<BuiltPipeline>();

        public GraphicsDevice Device { get; }
        public ResourceFactory Factory => Device.ResourceFactory;

        public PipelineCollection(GraphicsDevice device)
        {
            Device = device;
            AddShaderResourceAssemblyOf<PipelineCollection>();
        }

        protected override void DisposeManaged()
        {
            base.DisposeManaged();
            foreach (var pipeline in pipelines)
            {
                pipeline.Pipeline.Dispose();
                foreach (var resLayout in pipeline.Description.ResourceLayouts)
                    resLayout.Dispose();
            }
            pipelines.Clear();
            foreach (var shader in loadedShaders.Values.SelectMany(v => v))
                shader.Dispose();
            loadedShaders.Clear();
        }

        

        public IPipelineBuilder GetPipeline() => new PipelineBuilder(this);
    }
}
