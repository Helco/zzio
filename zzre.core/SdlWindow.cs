using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
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
    private readonly List<EventFilterFunc> eventFilters = new();
    private Window* window;
    private string title;
    private int width, height;

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

    public int Width => width;
    public int Height => height;

    public (int w, int h) Size
    {
        get => (width, height);
        set
        {
            CheckPointer();
            if (value == (width, height)) return;
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
        this.width = width;
        this.height = height;
        window = sdl.CreateWindow(title, Sdl.WindowposUndefined, Sdl.WindowposUndefined, width, height, (uint)flags);
        if (window == null)
            ThrowSdlError(nameof(Sdl.CreateWindow));
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
                width = ev.Window.Data1;
                height = ev.Window.Data2;
                OnResized?.Invoke(width, height);
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
            ThrowSdlError(nameof(Sdl.GetWindowWMInfo));

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
                    ThrowSdlError(nameof(Sdl.AndroidGetJNIEnv));
                return SwapchainSource.CreateAndroidSurface((nint)sysWmInfo.Info.Android.Surface, (nint)jniEnv);
            default:
                throw new PlatformNotSupportedException("Cannot create a SwapchainSource for " + sysWmInfo.Subsystem + ".");
        }
    }

    private void CheckPointer() =>
        ObjectDisposedException.ThrowIf(window == null, typeof(SdlWindow));

    private void ThrowSdlError(string context)
    {
        var exception = sdl.GetErrorAsException();
        if (exception == null)
            throw new SdlException("Unknown SDL error during " + context);
        else
            throw exception;
    }
}
