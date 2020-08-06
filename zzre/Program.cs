using System;
using System.Numerics;
using Veldrid;
using Veldrid.StartupUtilities;
using ImGuiNET;
using static ImGuiNET.ImGui;
using System.IO;
using zzre.imgui;
using zzre.core;
using zzio.vfs;
using zzre.tools;
using zzio;

namespace zzre
{
    class Program
    {
        static byte[] LoadShaderText(string name)
        {
            using var stream = typeof(Program).Assembly.GetManifestResourceStream("zzre.shaders." + name);
            if (stream == null)
                throw new FileNotFoundException($"Could not find embedded shader resource: {name}");
            var data = new byte[stream.Length];
            stream.Read(data.AsSpan());
            return data;
        }

        static Shader[] LoadShaders(ResourceFactory factory, string name) => new[]
        {
            factory.CreateShader(new ShaderDescription { EntryPoint = "main", ShaderBytes = LoadShaderText(name + ".frag"), Stage = ShaderStages.Fragment }),
            factory.CreateShader(new ShaderDescription { EntryPoint = "main", ShaderBytes = LoadShaderText(name + ".vert"), Stage = ShaderStages.Vertex })
        };

        static void Main(string[] args)
        {
            var window = VeldridStartup.CreateWindow(new WindowCreateInfo
            {
                X = 100,
                Y = 100,
                WindowWidth = 1024 * 3 / 2,
                WindowHeight = 768 * 3 / 2,
                WindowTitle = "Zanzarah"
            });
            var graphicsDevice = VeldridStartup.CreateGraphicsDevice(window, new GraphicsDeviceOptions
            {
                PreferDepthRangeZeroToOne = true,
                PreferStandardClipSpaceYDirection = true,
                SyncToVerticalBlank = true,
                Debug = true
            }, GraphicsBackend.Direct3D11);

            var factory = graphicsDevice.ResourceFactory;
            var pipelineCollection = new PipelineCollection(factory);
            pipelineCollection.AddShaderResourceAssemblyOf<Program>();
            var windowContainer = new WindowContainer(graphicsDevice);
            var resourcePool = new CombinedResourcePool(new IResourcePool[]
            {
                new PAKResourcePool(PAKArchive.ReadNew(new FileStream(@"C:\dev\zanzarah\Resources\DATA_0.PAK", FileMode.Open, FileAccess.Read))),
                new FileResourcePool(@"C:\dev\zanzarah")
            });
            var diContainer = new TagContainer();
            diContainer
                .AddTag(windowContainer)
                .AddTag(graphicsDevice)
                .AddTag<IResourcePool>(resourcePool)
                .AddTag(new StandardPipelines(pipelineCollection));

            var modelViewer = new ModelViewer(diContainer);
            modelViewer.LoadModel("resources/models/actorsex/chr01.dff");

            window.Resized += () =>
            {
                graphicsDevice.ResizeMainWindow((uint)window.Width, (uint)window.Height);
                windowContainer.HandleResize(window.Width, window.Height);
            };

            window.KeyDown += (ev) =>
            {
                windowContainer.HandleKeyEvent(ev.Key, ev.Down);
                if (ev.Down == true && ev.Key == Key.F5)
                    windowContainer.ImGuiRenderer.ResetContext(graphicsDevice, graphicsDevice.MainSwapchain.Framebuffer.OutputDescription);
            };

            var time = new GameTime();
            while (window.Exists)
            {
                time.BeginFrame();
                if (time.HasFramerateChanged)
                    window.Title = $"Zanzarah | {graphicsDevice.BackendType} | FPS: {(int)(time.Framerate + 0.5)}";

                windowContainer.Render();
                graphicsDevice.SwapBuffers();
                var inputSnapshot = window.PumpEvents();
                windowContainer.Update(time, inputSnapshot);

                time.EndFrame();
            }

            diContainer.Dispose();
        }
    }
}
