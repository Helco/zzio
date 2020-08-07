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
        private List<Assembly> shaderResourceAssemblies = new List<Assembly>();
        private Dictionary<string, Shader[]> loadedShaders = new Dictionary<string, Shader[]>();

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

        public void AddShaderResourceAssemblyOf<T>() => AddShaderResourceAssembly(typeof(T).Assembly);

        public void AddShaderResourceAssembly(Assembly assembly)
        {
            // Insert at front for higher priority without reversing
            shaderResourceAssemblies.Insert(0, assembly);
        }

        private Shader[] LoadShaderSet(string shaderSetName)
        {
            if (loadedShaders.TryGetValue(shaderSetName, out var set))
                return set;

            ShaderDescription LoadShader(string shaderName, ShaderStages stage)
            {
                using var stream = shaderResourceAssemblies
                    .Select(a => a.GetManifestResourceStream($"{a.GetName().Name}.shaders.{shaderName}"))
                    .FirstOrDefault(s => s != null);
                if (stream == null)
                    throw new FileNotFoundException($"Could not find embedded shader resource: {shaderName}");
                using var reader = new StreamReader(stream, true);
                var text = reader.ReadToEnd(); // reencode text because SPIRV does not support Unicode BOMs

                return new ShaderDescription
                {
                    EntryPoint = "main",
                    ShaderBytes = System.Text.Encoding.UTF8.GetBytes(text),
                    Stage = stage
                };
            }
            return Factory.CreateFromSpirv(
                LoadShader(shaderSetName + ".vert", ShaderStages.Vertex),
                LoadShader(shaderSetName + ".frag", ShaderStages.Fragment));
        }

        public IPipelineBuilder GetPipeline() => new PipelineBuilder(this);
    }
}
