using System;
using System.Numerics;
using Veldrid;
using Veldrid.StartupUtilities;
using ImGuiNET;
using System.Reflection;
using static ImGuiNET.ImGui;

namespace zzre
{
    class Program
    {
        static byte[] LoadShaderText(string name)
        {
            using var stream = typeof(Program).Assembly.GetManifestResourceStream("zzre.shaders." + name);
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
            var imguiRenderer = new Veldrid.ImGuiRenderer(graphicsDevice, graphicsDevice.MainSwapchain.Framebuffer.OutputDescription, window.Width, window.Height);
            var imguiCommandList = factory.CreateCommandList();

            LoadShaders(factory, "color");

            window.Resized += () =>
                {
                    graphicsDevice.ResizeMainWindow((uint)window.Width, (uint)window.Height);
                    imguiRenderer.WindowResized(window.Width, window.Height);
                };

            var time = new GameTime();
            while (window.Exists)
            {
                time.BeginFrame();
                if (time.HasFramerateChanged)
                    window.Title = $"Zanzarah | {graphicsDevice.BackendType} | FPS: {(int)(time.Framerate + 0.5)}";

                var viewport = GetMainViewport();
                SetNextWindowPos(viewport.Pos);
                SetNextWindowSize(viewport.Size);
                SetNextWindowViewport(viewport.ID);
                PushStyleVar(ImGuiStyleVar.WindowRounding, 0.0f);
                PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0.0f);
                PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
                Begin("Master",
                    ImGuiWindowFlags.NoTitleBar |
                    ImGuiWindowFlags.NoCollapse |
                    ImGuiWindowFlags.NoResize |
                    ImGuiWindowFlags.NoMove |
                    ImGuiWindowFlags.NoBringToFrontOnFocus |
                    ImGuiWindowFlags.NoNavFocus |
                    ImGuiWindowFlags.MenuBar);
                PopStyleVar(3);
                if (BeginMenuBar())
                {
                    if (MenuItem("Test item"))
                        Console.WriteLine("testy test test");
                    EndMenuBar();
                }
                DockSpace(GetID("MasterDockSpace"), Vector2.Zero, ImGuiDockNodeFlags.NoDockingInCentralNode);
                End();
                ShowDemoWindow();
                imguiCommandList.Begin();
                imguiCommandList.SetFramebuffer(graphicsDevice.MainSwapchain.Framebuffer);
                imguiCommandList.ClearColorTarget(0, RgbaFloat.Cyan);
                imguiRenderer.Render(graphicsDevice, imguiCommandList);
                imguiCommandList.End();
                graphicsDevice.SubmitCommands(imguiCommandList);

                graphicsDevice.SwapBuffers();
                var inputSnapshot = window.PumpEvents();
                imguiRenderer.Update(time.Delta, inputSnapshot);

                time.EndFrame();
            }

            graphicsDevice.Dispose();
        }
    }
}
