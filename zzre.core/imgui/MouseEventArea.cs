using System;
using System.Numerics;
using Silk.NET.SDL;
using ImGuiNET;
using static ImGuiNET.ImGui;

namespace zzre.imgui;

public class MouseEventArea
{
    private readonly Window window;
    private Vector2? lastPosition;
    private readonly Vector2[] lastDragDelta = new Vector2[(int)ImGuiMouseButton.COUNT];
    private readonly bool[] triggerClickEvent = new bool[(int)ImGuiMouseButton.COUNT];
    private Rect validBounds = Rect.Zero;

    public Vector2 MousePosition => GetIO().MousePos - validBounds.Min;

    public event Action<MouseButton, Vector2>? OnDrag;
    public event Action<float>? OnScroll;
    public event Action<MouseButton, Vector2>? OnButtonDown;
    public event Action<MouseButton, Vector2>? OnButtonUp;
    public event Action<Vector2>? OnMove;

    public MouseEventArea(Window parent)
    {
        window = parent;
        parent.AddTag(this);
    }

    public void Content()
    {
        if (!window.IsOpen || !window.IsFocused)
            return;

        var offset = GetCursorScreenPos();
        var size = GetContentRegionAvail();
        validBounds = new Rect(offset + size / 2, size);

        // Scroll event
        bool isCurrentlyInside = validBounds.IsInside(GetIO().MousePos);
        if (isCurrentlyInside && MathF.Abs(GetIO().MouseWheel) > 0.01f)
            OnScroll?.Invoke(GetIO().MouseWheel);

        // Move event
        if (isCurrentlyInside)
        {
            var newPosition = MousePosition;
            if (lastPosition != null)
                OnMove?.Invoke(newPosition - lastPosition.Value);
            lastPosition = MousePosition;
        }
        else
            lastPosition = null;

        // Drag event
        for (int i = 0; i < (int)ImGuiMouseButton.COUNT; i++)
        {
            var button = (ImGuiMouseButton)i;
            if (!validBounds.IsInside(GetIO().MouseClickedPos[i]))
                continue;

            if (triggerClickEvent[i] != IsMouseDown(button))
            {
                ((triggerClickEvent[i] = IsMouseDown(button))
                    ? OnButtonDown
                    : OnButtonUp)?.Invoke(ToSDL(button), validBounds.RelativePos(GetIO().MouseClickedPos[i]));
            }

            if (!IsMouseDragging(button))
            {
                lastDragDelta[i] = Vector2.Zero;
                continue;
            }

            var delta = GetMouseDragDelta(button);
            OnDrag?.Invoke(ToSDL(button), delta - lastDragDelta[i]);
            lastDragDelta[i] = delta;
        }
    }

    private static MouseButton ToSDL(ImGuiMouseButton btn) => btn switch
    {
        ImGuiMouseButton.Left => MouseButton.Left,
        ImGuiMouseButton.Right => MouseButton.Right,
        ImGuiMouseButton.Middle => MouseButton.Middle,
        _ => throw new NotSupportedException($"Unsupported imgui mouse button: {btn}")
    };
}
