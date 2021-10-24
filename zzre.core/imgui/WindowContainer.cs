﻿using ImGuiNET;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using Veldrid;
using static ImGuiNET.ImGui;

namespace zzre.imgui
{
    public class WindowContainer : BaseDisposable, IReadOnlyCollection<BaseWindow>
    {
        private GraphicsDevice Device { get; }
        private ResourceFactory Factory => Device.ResourceFactory;
        private List<BaseWindow> windows = new List<BaseWindow>();
        private List<Fence> onceFences = new List<Fence>();
        private CommandList commandList;
        private Fence fence;

        public BaseWindow? FocusedWindow { get; private set; } = null;
        public int Count => windows.Count;
        public ImGuiRenderer ImGuiRenderer { get; }
        public MenuBar MenuBar { get; } = new MenuBar();
        private bool isInUpdateEnumeration = false;
        private OnceAction onceAfterUpdate = new OnceAction();
        private BaseWindow? nextFocusedWindow = null;

        public event Action OnceAfterUpdate
        {
            add => onceAfterUpdate.Next += value;
            remove => onceAfterUpdate.Next -= value;
        }

        public WindowContainer(GraphicsDevice device)
        {
            Device = device;

            var fb = device.MainSwapchain.Framebuffer;
            ImGuiRenderer = new ImGuiRenderer(device, fb.OutputDescription, (int)fb.Width, (int)fb.Height);
            ImGuizmoNET.ImGuizmo.SetImGuiContext(ImGui.GetCurrentContext());
            ImGuizmoNET.ImGuizmo.AllowAxisFlip(false);
            commandList = Factory.CreateCommandList();
            fence = Factory.CreateFence(true);

            LoadForkAwesomeFont();
            ImGuiRenderer.Start();
        }

        protected override void DisposeManaged()
        {
            base.DisposeManaged();
            foreach (var window in this.ToArray())
                window.Dispose();
            ImGuiRenderer.Dispose();
            commandList.Dispose();
            fence.Dispose();
        }

        private void AddSafelyToWindows(BaseWindow window)
        {
            if (isInUpdateEnumeration)
                OnceAfterUpdate += () => windows.Add(window);
            else
                windows.Add(window);
        }

        public Window NewWindow(string title = "Window")
        {
            var window = new Window(this, title);
            AddSafelyToWindows(window);
            return window;
        }

        public Modal NewModal(string title = "Modal")
        {
            var modal = new Modal(this, title);
            AddSafelyToWindows(modal);
            return modal;
        }

        public void Update(GameTime time, InputSnapshot input)
        {
            ImGuiRenderer.Update(time.Delta, input);
            ImGuizmoNET.ImGuizmo.BeginFrame();

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
            MenuBar.Update();
            End();

#if DEBUG
            ShowDemoWindow();
#endif
            if (nextFocusedWindow != null && !IsMouseDown(ImGuiMouseButton.Left))
            {
                SetWindowFocus(nextFocusedWindow.Title);
                nextFocusedWindow = null;
            }
            FocusedWindow = null;
            isInUpdateEnumeration = true;
            foreach (var window in this)
            {
                window.Update();
                if (window.IsFocused)
                    FocusedWindow = window;
            }
            foreach (var window in this.OfType<Window>().Where(w => !w.IsOpen).ToArray())
                window.Dispose();
            isInUpdateEnumeration = false;
            onceAfterUpdate.Invoke();
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

        public void RemoveWindow(BaseWindow window) => windows.Remove(window);
        public void AddFenceOnce(Fence fence) => onceFences.Add(fence);
        public BaseWindow? WithTag<TTag>() where TTag : class => windows.FirstOrDefault(w => w.HasTag<TTag>());
        public IEnumerable<BaseWindow> AllWithTag<TTag>() where TTag : class => windows.Where(w => w.HasTag<TTag>());
        public IEnumerator<BaseWindow> GetEnumerator() => windows.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => windows.GetEnumerator();

        private unsafe void LoadForkAwesomeFont()
        {
            zzre.core.assets.ForkAwesomeIconFont.AddToFontAtlas(GetIO().Fonts, 1, 15.0f, 13.0f);
            ImGuiRenderer.RecreateFontDeviceTexture();
        }

        public void SetNextFocusedWindow(BaseWindow window) => nextFocusedWindow = window;
    }
}
