using System;
using System.Collections.Generic;
using Silk.NET.SDL;
using Veldrid;
using zzio;

namespace zzre;

public enum MouseButton
{
    Left = Sdl.ButtonLeft,
    Middle = Sdl.ButtonMiddle,
    Right = Sdl.ButtonRight,
    X1 = Sdl.ButtonX1,
    X2 = Sdl.ButtonX2
}

public unsafe class SdlWindow : BaseDisposable
{
    public delegate bool EventFilterFunc(SdlWindow window, Event ev);

    private readonly Sdl sdl;
    private readonly List<EventFilterFunc> eventFilters = [];
    private Window* window;
    private string title;

    public uint WindowID { get; }
    public bool IsOpen => window != null;

    public string Title
    {
        get => title;
        set
        {
            CheckPointer();
            if (title == value) return;
            title = value;
            sdl.SetWindowTitle(window, title);
        }
    }

    public int Width { get; private set; }
    public int Height { get; private set; }

    public (int w, int h) Size
    {
        get => (Width, Height);
        set
        {
            CheckPointer();
            if (value == (Width, Height)) return;
            sdl.SetWindowSize(window, value.w, value.h);
        }
    }

    public event EventFilterFunc EventFilter
    {
        add => eventFilters.Add(value);
        remove => eventFilters.Remove(value);
    }

    public event Action<KeyboardEvent>? OnKey;
    public event Action<MouseButtonEvent>? OnMouseButton;
    public event Action<MouseWheelEvent>? OnMouseWheel;
    public event Action<int, int>? OnResized;
    
    public SdlWindow(Sdl sdl, string title, int width, int height, WindowFlags flags)
    {
        this.sdl = sdl;
        this.title = title;
        this.Width = width;
        this.Height = height;
        window = sdl.CreateWindow(title, Sdl.WindowposUndefined, Sdl.WindowposUndefined, width, height, (uint)flags);
        if (window == null)
            sdl.ThrowError(nameof(Sdl.CreateWindow));
        WindowID = sdl.GetWindowID(window);
    }

    protected override void DisposeManaged()
    {
        base.DisposeManaged();
        if (window != null)
        {
            sdl.DestroyWindow(window);
            window = null;
        }
    }

    public bool HandleEvent(Event ev)
    {
        CheckPointer();
        foreach (var filter in eventFilters)
        {
            if (filter(this, ev))
                return true;
        }
        switch((EventType)ev.Type)
        {
            case EventType.Windowevent when ev.Window.WindowID == WindowID:
                HandleWindowEvent(ev);
                return true;
            case EventType.Keydown when ev.Key.WindowID == WindowID:
            case EventType.Keyup when ev.Key.WindowID == WindowID:
                OnKey?.Invoke(ev.Key);
                return true;
            case EventType.Mousebuttondown when ev.Button.WindowID == WindowID:
            case EventType.Mousebuttonup when ev.Button.WindowID == WindowID:
                OnMouseButton?.Invoke(ev.Button);
                return true;
            case EventType.Mousewheel when ev.Wheel.WindowID == WindowID:
                OnMouseWheel?.Invoke(ev.Wheel);
                return true;
        }
        return false;
    }

    private void HandleWindowEvent(Event ev)
    {
        switch((WindowEventID)ev.Window.Event)
        {
            case WindowEventID.SizeChanged:
                Width = ev.Window.Data1;
                Height = ev.Window.Data2;
                OnResized?.Invoke(Width, Height);
                break;
            case WindowEventID.Close:
                Dispose();
                break;
        }
    }

    public SwapchainSource CreateSwapchainSource()
    {
        CheckPointer();
        SysWMInfo sysWmInfo = default;
        sdl.GetVersion(ref sysWmInfo.Version);
        if (!sdl.GetWindowWMInfo(window, &sysWmInfo))
            sdl.ThrowError(nameof(Sdl.GetWindowWMInfo));

        switch (sysWmInfo.Subsystem)
        {
            case SysWMType.Windows:
                return SwapchainSource.CreateWin32(sysWmInfo.Info.Win.Hwnd, sysWmInfo.Info.Win.HInstance);
            case SysWMType.X11:
                return SwapchainSource.CreateXlib((nint)sysWmInfo.Info.X11.Display, (nint)sysWmInfo.Info.X11.Window);
            case SysWMType.Wayland:
                return SwapchainSource.CreateWayland((nint)sysWmInfo.Info.Wayland.Display, (nint)sysWmInfo.Info.Wayland.Surface);
            case SysWMType.Cocoa:
                return SwapchainSource.CreateNSWindow((nint)sysWmInfo.Info.Cocoa.Window);
            case SysWMType.Android:
                var jniEnv = sdl.AndroidGetJNIEnv();
                if (jniEnv == null)
                    sdl.ThrowError(nameof(Sdl.AndroidGetJNIEnv));
                return SwapchainSource.CreateAndroidSurface((nint)sysWmInfo.Info.Android.Surface, (nint)jniEnv);
            default:
                throw new PlatformNotSupportedException("Cannot create a SwapchainSource for " + sysWmInfo.Subsystem + ".");
        }
    }

    private void CheckPointer() =>
        ObjectDisposedException.ThrowIf(window == null, typeof(SdlWindow));
}
