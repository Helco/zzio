using ImGuiNET;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Veldrid;
using static ImGuiNET.ImGui;

namespace zzre.imgui
{
    public class WindowContainer : BaseDisposable, IReadOnlyCollection<Window>
    {
        private GraphicsDevice Device { get; }
        private ResourceFactory Factory => Device.ResourceFactory;
        private List<Window> windows = new List<Window>();
        private List<Fence> onceFences = new List<Fence>();
        private CommandList commandList;
        private Fence fence;

        public Window? FocusedWindow { get; private set; } = null;
        public int Count => windows.Count;
        public ImGuiRenderer ImGuiRenderer { get; }
        public Action OnMenuBar = () => { };

        public WindowContainer(GraphicsDevice device)
        {
            Device = device;

            var fb = device.MainSwapchain.Framebuffer;
            ImGuiRenderer = new ImGuiRenderer(device, fb.OutputDescription, (int)fb.Width, (int)fb.Height);
            commandList = Factory.CreateCommandList();
            fence = Factory.CreateFence(true);
        }

        protected override void DisposeManaged()
        {
            base.DisposeManaged();
            foreach (var window in this)
                window.Dispose();
            ImGuiRenderer.Dispose();
            commandList.Dispose();
            fence.Dispose();
        }

        public Window NewWindow(string title = "Window")
        {
            var window = new Window(this, title);
            windows.Add(window);
            return window;
        }

        public void Update(GameTime time, InputSnapshot input)
        {
            ImGuiRenderer.Update(time.Delta, input);

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
            DockSpace(GetID("MasterDockSpace"));
            if (BeginMenuBar())
            {
                OnMenuBar();
                EndMenuBar();
            }
            End();

            ShowDemoWindow();
            FocusedWindow = null;
            foreach (var window in this)
            {
                window.Update();
                if (window.IsFocused)
                {
                    FocusedWindow = window;
                    window.HandleMouseEvents();
                }
            }
        }

        public void Render()
        {
            foreach (var window in this)
                window.HandleRender();
            if (onceFences.Count > 0)
                Device.WaitForFences(onceFences.ToArray(), true, TimeSpan.FromSeconds(10000.0)); // timeout is a workaround
            onceFences.Clear();

            fence.Reset();
            commandList.Begin();
            commandList.SetFramebuffer(Device.MainSwapchain.Framebuffer);
            commandList.ClearColorTarget(0, RgbaFloat.Cyan);
            ImGuiRenderer.Render(Device, commandList);
            commandList.End();
            Device.SubmitCommands(commandList, fence);
        }

        public void HandleKeyEvent(Key sym, bool isDown)
        {
            if (!GetIO().WantCaptureKeyboard)
                FocusedWindow?.HandleKeyEvent(sym, isDown);
        }

        public void HandleResize(int width, int height)
        {
            ImGuiRenderer.WindowResized(width, height);
        }

        public void RemoveWindow(Window window) => windows.Remove(window);
        public void AddFenceOnce(Fence fence) => onceFences.Add(fence);
        public Window? WithTag<TTag>() where TTag : class => windows.FirstOrDefault(w => w.HasTag<TTag>());
        public IEnumerable<Window> AllWithTag<TTag>() where TTag : class => windows.Where(w => w.HasTag<TTag>());
        public IEnumerator<Window> GetEnumerator() => windows.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => windows.GetEnumerator();
    }
}
