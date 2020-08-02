using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ImGuiNET;
using static ImGuiNET.ImGui;
using Veldrid;

namespace zzre.imgui
{
    public enum WindowOpenState
    {
        Open,
        Closed,
        Unclosable
    }

    public class Window : TagContainer
    {
        private Vector2[] lastDragDelta = new Vector2[(int)ImGuiMouseButton.COUNT];
        private float toolbarHeight;

        public WindowContainer Container { get; }
        public Rect InitialBounds { get; set; } = new Rect(new Vector2(float.NaN, float.NaN), Vector2.One * 300);
        public Rect Bounds { get; set; }
        public Rect ContentBounds => Bounds.GrownBy(0.0f, -toolbarHeight);
        public string Title { get; set; }
        public ImGuiWindowFlags Flags { get; set; }
        public WindowOpenState OpenState { get; set; } = WindowOpenState.Open;
        public bool IsOpen => OpenState != WindowOpenState.Closed;
        public bool IsFocused { get; set; } = false;

        public event Action OnRender = () => { };
        public event Action OnBeforeContent = () => { };
        public event Action OnContent = () => { };
        public event Action<ImGuiMouseButton, Vector2> OnDrag = (_, __) => { };
        public event Action<float> OnScroll = _ => { };
        public event Action<Key> OnKeyDown = _ => { };
        public event Action<Key> OnKeyUp = _ => { };

        public Window(WindowContainer container, string title = "Window")
        {
            Container = container;
            Title = title;
        }

        protected override void DisposeManaged()
        {
            base.DisposeManaged();
            Container.RemoveWindow(this);
        }

        public void Update()
        {
            if (OpenState == WindowOpenState.Closed)
                return;
            OnBeforeContent();

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
            toolbarHeight = GetItemRectSize().Y;

            OnContent();
            End();
        }

        public void HandleMouseEvents()
        {
            // Scroll event
            if (Bounds.IsInside(GetIO().MousePos) && MathF.Abs(GetIO().MouseWheel) > 0.01f)
                OnScroll(GetIO().MouseWheel);

            // Drag event
            for (int i = 0; i < (int)ImGuiMouseButton.COUNT; i++)
            {
                var button = (ImGuiMouseButton)i;
                if (!IsMouseDragging(button) || !Bounds.IsInside(GetIO().MouseClickedPos[i]))
                    continue;

                var delta = GetMouseDragDelta(button);
                OnDrag(button, delta - lastDragDelta[i]);
                lastDragDelta[i] = delta;
            }
        }

        public void HandleKeyEvent(Key sym, bool isDown) => (isDown ? OnKeyDown : OnKeyUp)(sym);
        public void HandleRender() => OnRender();
    }
}
