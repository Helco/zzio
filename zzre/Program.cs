#if !DEBUG
using System;
#endif
using System.IO;
using Veldrid;
using Veldrid.StartupUtilities;
using zzre.imgui;
using zzio.vfs;
using zzre.tools;
using zzre.rendering;

namespace zzre
{
    internal class Program
    {
        // We preload some assemblies as they are likely to be loaded during gameplay
        // which causes very noticeable hickups.
        // Nevertheless this is symptom based fixing and this list should be checked
        // regularly and on different platforms.
        private static readonly string[] PreloadAssemblies = new[]
        {
            "System.Text.RegularExpressions",
            "System.Reflection.Emit.Lightweight",
            "System.Reflection.Emit.ILGeneration",
            "System.Reflection.Primitives",
            "Veldrid.ImageSharp"
        };

        private static void Main(string[] args)
        {
            System.Array.ForEach(PreloadAssemblies, n => System.Reflection.Assembly.Load(n));

            System.Threading.Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
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
#if DEBUG
                new PAKResourcePool(new FileStream(@"C:\dev\zanzarah\Resources\DATA_0.PAK", FileMode.Open, FileAccess.Read)),
                new FileResourcePool(@"C:\dev\zanzarah\")
#else
                new PAKResourcePool(new FileStream(Path.Combine(Environment.CurrentDirectory, "..", "Resources", "DATA_0.PAK"), FileMode.Open, FileAccess.Read)),
                new FileResourcePool(Path.Combine(Environment.CurrentDirectory, ".."))
#endif
            });
            var time = new GameTime();
            var diContainer = new TagContainer();
            diContainer
                .AddTag(time)
                .AddTag(windowContainer)
                .AddTag(graphicsDevice)
                .AddTag(graphicsDevice.ResourceFactory)
                .AddTag<IResourcePool>(resourcePool)
                .AddTag(pipelineCollection)
                .AddTag<IAssetLoader<Texture>>(new TextureAssetLoader(diContainer))
                .AddTag(new OpenDocumentSet(diContainer))
                .AddTag(IconFont.CreateForkAwesome(graphicsDevice));

            windowContainer.MenuBar.AddButton("Tools/Model Viewer", () => new ModelViewer(diContainer));
            windowContainer.MenuBar.AddButton("Tools/Actor Viewer", () => new ActorEditor(diContainer));
            windowContainer.MenuBar.AddButton("Tools/Effect Viewer", () => new EffectEditor(diContainer));
            windowContainer.MenuBar.AddButton("Tools/World Viewer", () => new WorldViewer(diContainer));
            windowContainer.MenuBar.AddButton("Tools/Scene Viewer", () => new SceneEditor(diContainer));

#if DEBUG
            new ZanzarahWindow(diContainer);
#endif

            window.Resized += () =>
            {
                graphicsDevice.ResizeMainWindow((uint)window.Width, (uint)window.Height);
                windowContainer.HandleResize(window.Width, window.Height);
            };

            window.KeyDown += (ev) =>
            {
                if (ev.Repeat)
                    return;
                windowContainer.HandleKeyEvent(ev.Key, ev.Down);
                if (ev.Key == Key.F5)
                    windowContainer.ImGuiRenderer.ResetContext(graphicsDevice, graphicsDevice.MainSwapchain.Framebuffer.OutputDescription);
            };

            window.KeyUp += (ev) =>
            {
                if (ev.Repeat)
                    return;
                windowContainer.HandleKeyEvent(ev.Key, ev.Down);
            };

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

            // dispose graphics device last, otherwise Vulkan will crash
            diContainer.RemoveTag<GraphicsDevice>();
            diContainer.Dispose();
            graphicsDevice.Dispose();
        }
    }
}
