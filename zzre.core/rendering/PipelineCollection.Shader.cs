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

            var backendExt = Device.BackendType switch
            {
                GraphicsBackend.Vulkan => ".spv",
                GraphicsBackend.Direct3D11 => ".hlsl",
                GraphicsBackend.Metal => ".msl",
                GraphicsBackend.OpenGL => ".glsl",
                GraphicsBackend.OpenGLES => ".essl",
                _ => "dummy"
            };

            var shaderDescr = TryLoadShaderSet(shaderSetName, "_Vertex", "_Fragment", backendExt);
            if (false && shaderDescr.HasValue)
                return new[]
                {
                    Factory.CreateShader(shaderDescr.Value.vertex),
                    Factory.CreateShader(shaderDescr.Value.fragment),
                };

            //if (Device.BackendType != GraphicsBackend.Vulkan) // we would have tried to load SPIRV already
             //   shaderDescr = TryLoadShaderSet(shaderSetName, "_Vertex", "_Fragment", ".spv");
            shaderDescr ??= TryLoadShaderSet(shaderSetName, ".vert", ".frag");
            if (!shaderDescr.HasValue)
                throw new FileNotFoundException($"Could not find embedded shader resource: {shaderSetName}");

            return Factory.CreateFromSpirv(shaderDescr.Value.vertex, shaderDescr.Value.fragment);
        }

        private (ShaderDescription vertex, ShaderDescription fragment)? TryLoadShaderSet(string shaderSetName, string vertexExt, string fragmentExt, string commonExt = "")
        {
            var vertexDescr = TryLoadShader($"{shaderSetName}{vertexExt}{commonExt}", ShaderStages.Vertex);
            var fragmentDescr = TryLoadShader($"{shaderSetName}{fragmentExt}{commonExt}", ShaderStages.Fragment);
            return vertexDescr.HasValue && fragmentDescr.HasValue
                ? (vertexDescr.Value, fragmentDescr.Value)
                : null;
        }

        private ShaderDescription? TryLoadShader(string shaderName, ShaderStages stage)
        {
            using var stream = shaderResourceAssemblies
                .Select(a => a.GetManifestResourceStream($"{a.GetName().Name}.shaders.{shaderName}"))
                .FirstOrDefault(s => s != null);
            if (stream == null)
                return null;
            using var reader = new StreamReader(stream, true);
            var text = reader.ReadToEnd(); // reencode text because SPIRV does not support Unicode BOMs

            return new ShaderDescription
            {
                EntryPoint = "main",
                ShaderBytes = System.Text.Encoding.UTF8.GetBytes(text),
                Stage = stage
            };
        }
    }
}
