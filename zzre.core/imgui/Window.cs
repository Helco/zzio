using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ImGuiNET;
using static ImGuiNET.ImGui;

namespace zzre.imgui
{
    public enum WindowOpenState
    {
        Open,
        Closed,
        Unclosable
    }

    public class Window : BaseWindow
    {
        public Rect InitialBounds { get; set; } = new Rect(new Vector2(float.NaN, float.NaN), Vector2.One * 300);
        public Rect Bounds { get; set; }
        public WindowOpenState OpenState { get; set; } = WindowOpenState.Open;
        public override bool IsOpen => OpenState != WindowOpenState.Closed;

        public Window(WindowContainer container, string title = "Window") : base(container, title) { }

        public override void Update()
        {
            IsFocused = false;
            if (OpenState == WindowOpenState.Closed)
                return;
            RaiseBeforeContent();

            bool isOpen = true;
            if (float.IsFinite(InitialBounds.Min.X) && float.IsFinite(InitialBounds.Min.Y))
                SetNextWindowPos(InitialBounds.Min, ImGuiCond.FirstUseEver, Vector2.Zero);
            if (float.IsFinite(InitialBounds.Size.X) && float.IsFinite(InitialBounds.Size.Y))
                SetNextWindowSize(InitialBounds.Size, ImGuiCond.FirstUseEver);

            Begin(Title, ref isOpen, Flags);
            if (!isOpen && OpenState == WindowOpenState.Open)
                OpenState = WindowOpenState.Closed;

            Rect newBounds = new Rect();
            newBounds.Min = GetCursorScreenPos();
            newBounds.Max = newBounds.Min + GetWindowSize();
            Bounds = newBounds;
            IsFocused = IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows);

            RaiseContent();
            End();
        }
    }
}
