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
using zzre.rendering;
using System.Net.WebSockets;

namespace zzre
{
    class Program
    {
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

            var pipelineCollection = new PipelineCollection(graphicsDevice);
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
                .AddTag(pipelineCollection)
                .AddTag(new TextureLoader(diContainer));

            var actorEditor = new ActorEditor(diContainer);
            actorEditor.LoadActor(@"resources/models/actorsex/chr01.aed");

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
