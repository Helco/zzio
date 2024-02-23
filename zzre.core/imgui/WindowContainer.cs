using ImGuiNET;
using Silk.NET.SDL;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Veldrid;
using zzio;
using static ImGuiNET.ImGui;

namespace zzre.imgui;

public class WindowContainer : BaseDisposable, IReadOnlyCollection<BaseWindow>
{
    private GraphicsDevice Device { get; }
    private ResourceFactory Factory => Device.ResourceFactory;
    private readonly List<BaseWindow> windows = [];
    private readonly List<Fence> onceFences = [];
    private readonly CommandList commandList;
    private readonly Fence fence;

    public BaseWindow? FocusedWindow { get; private set; }
    public int Count => windows.Count;
    public ImGuiRenderer ImGuiRenderer { get; }
    public MenuBar MenuBar { get; } = new();
    public ref bool ShowImGuiDemoWindow => ref showImGuiDemoWindow;

    private bool isInUpdateEnumeration;
    private bool showImGuiDemoWindow;
    private readonly OnceAction onceBeforeUpdate = new();
    private readonly OnceAction onceAfterUpdate = new();
    private BaseWindow? nextFocusedWindow;

    public event Action OnceBeforeUpdate
    {
        add => onceBeforeUpdate.Next += value;
        remove => onceBeforeUpdate.Next -= value;
    }

    public event Action OnceAfterUpdate
    {
        add => onceAfterUpdate.Next += value;
        remove => onceAfterUpdate.Next -= value;
    }

    public Func<string, IDisposable>? CreateProfilerSample { get; set; }

    public WindowContainer(SdlWindow window, GraphicsDevice device)
    {
        Device = device;

        var fb = device.MainSwapchain!.Framebuffer;
        ImGuiRenderer = new(device, fb.OutputDescription, (int)fb.Width, (int)fb.Height, ColorSpaceHandling.Legacy, callNewFrame: false);
        ImGuizmoNET.ImGuizmo.SetImGuiContext(ImGui.GetCurrentContext());
        ImGuizmoNET.ImGuizmo.AllowAxisFlip(false);
        commandList = Factory.CreateCommandList();
        fence = Factory.CreateFence(true);

        var io = GetIO();
        io.ConfigFlags |= ImGuiConfigFlags.DockingEnable;
        io.ConfigWindowsResizeFromEdges = true;
        io.ConfigWindowsMoveFromTitleBarOnly = true;

        ImGuiRenderer.UseWith(window);
        window.EventFilter += HandleEvent;

        LoadForkAwesomeFont();
        ImGuiRenderer.ManualNewFrame();
    }

    protected override void DisposeManaged()
    {
        base.DisposeManaged();
        foreach (var window in this.ToArray())
            window.Dispose();
        ImGuiRenderer.Dispose();
        commandList.Dispose();
        if (!fence.Signaled)
            Device.WaitForFence(fence);
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

    public void BeginEventUpdate(GameTime time)
    {
        using var _ = CreateProfilerSample?.Invoke("Dear Imgui");
        onceBeforeUpdate.Invoke();
        ImGuiRenderer.BeginEventUpdate(time.Delta);
    }

    private bool HandleEvent(SdlWindow window, Event ev)
    {
        if ((EventType)ev.Type is EventType.Keydown or EventType.Keyup && (KeyCode)ev.Key.Keysym.Sym is not KeyCode.KPrintscreen)
        {
            FocusedWindow?.HandleKeyEvent((KeyCode)ev.Key.Keysym.Sym, (EventType)ev.Type is EventType.Keydown);
            return FocusedWindow != null;
        }
        return false;
    }

    public void EndEventUpdate()
    {
        ImGuiRenderer.EndEventUpdate();
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

        if (showImGuiDemoWindow)
            ShowDemoWindow(ref showImGuiDemoWindow);
        onceAfterUpdate.Invoke();
    }

    public void Render()
    {
        using (CreateProfilerSample?.Invoke("Windows.Submit"))
        {
            foreach (var window in this)
                window.HandleRender();
        }
        using (CreateProfilerSample?.Invoke("Windows.Finish"))
        {
            if (onceFences.Count > 0)
                Device.WaitForFences([.. onceFences], true, TimeSpan.FromSeconds(10000.0)); // timeout is a workaround
            onceFences.Clear();
        }
        using (CreateProfilerSample?.Invoke("Container"))
        {
            if (!fence.Signaled)
                Device.WaitForFence(fence);
            fence.Reset();
            commandList.Begin();
            commandList.SetFramebuffer(Device.MainSwapchain!.Framebuffer);
            commandList.ClearColorTarget(0, RgbaFloat.Cyan);
            ImGuiRenderer.Render(Device, commandList);
            commandList.End();
            Device.SubmitCommands(commandList, fence);
        }
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
        Device.WaitForIdle();
        ImGuiRenderer.RecreateFontDeviceTexture();
    }

    public void SetNextFocusedWindow(BaseWindow window) => nextFocusedWindow = window;
}
