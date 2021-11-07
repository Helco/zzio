using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Veldrid;
using Veldrid.SPIRV;

namespace zzre.rendering
{
    public partial class PipelineCollection
    {
        private readonly List<Assembly> shaderResourceAssemblies = new List<Assembly>();
        private readonly Dictionary<string, Shader[]> loadedShaders = new Dictionary<string, Shader[]>();

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
    }
}
