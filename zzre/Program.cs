using System;
using System.Numerics;
using Veldrid;
using Veldrid.StartupUtilities;
using ImGuiNET;
using static ImGuiNET.ImGui;
using System.IO;
using zzre.imgui;
using zzre.core;

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
                WindowWidth = 1024,
                WindowHeight = 768,
                WindowTitle = "Zanzarah"
            });
            var graphicsDevice = VeldridStartup.CreateGraphicsDevice(window, new GraphicsDeviceOptions
            {
                PreferDepthRangeZeroToOne = true,
                PreferStandardClipSpaceYDirection = true,
                SyncToVerticalBlank = true
            }, GraphicsBackend.Vulkan);

            var factory = graphicsDevice.ResourceFactory;
            var pipelineCollection = new PipelineCollection(factory);
            pipelineCollection.AddShaderResourceAssemblyOf<Program>();
            var windowContainer = new WindowContainer(graphicsDevice);
            var fbWindow = windowContainer.NewWindow("Framebuffer Window");
            var fbWindowTag = new FramebufferWindowTag(fbWindow, graphicsDevice);
            fbWindowTag.Pipeline = pipelineCollection.GetPipeline()
                .WithShaderSet("color")
                .WithDepthTarget(PixelFormat.D24_UNorm_S8_UInt)
                .WithColorTarget(PixelFormat.R8_G8_B8_A8_UNorm)
                .With("Position", VertexElementFormat.Float3, VertexElementSemantic.Position)
                .With("UniformBuffer", ResourceKind.UniformBuffer, ShaderStages.Fragment)
                .Build().Pipeline;
            fbWindowTag.OnRender += cmdList => cmdList.ClearColorTarget(0, RgbaFloat.Red);
            var fbWindowMenu = new MenuBarWindowTag(fbWindow);
            fbWindowMenu.AddItem("Root Clicky", () => { Console.WriteLine("root clicky"); });
            fbWindowMenu.AddItem("Not/So/Root/Clicky", () => { Console.WriteLine("not so root clicky"); });
            fbWindowMenu.AddItem("Not/So/Fast", () => { Console.WriteLine("not so fast"); });

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

            pipelineCollection.Dispose();
            graphicsDevice.Dispose();
        }
    }
}
