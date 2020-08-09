﻿using System;
using System.Collections.Generic;
using System.Numerics;
using ImGuiNET;
using static ImGuiNET.ImGui;

namespace zzre.imgui
{
    public class MouseEventArea
    {
        private readonly Window window;
        private Vector2[] lastDragDelta = new Vector2[(int)ImGuiMouseButton.COUNT];

        public event Action<ImGuiMouseButton, Vector2> OnDrag = (_, __) => { };
        public event Action<float> OnScroll = _ => { };

        public MouseEventArea(Window parent)
        {
            window = parent;
        }

        public void Content()
        {
            if (!window.IsOpen || !window.IsFocused)
                return;

            var offset = GetCursorScreenPos();
            var size = GetContentRegionAvail();
            var validBounds = new Rect(offset + size / 2, size);

            // Scroll event
            if (validBounds.IsInside(GetIO().MousePos) && MathF.Abs(GetIO().MouseWheel) > 0.01f)
                OnScroll(GetIO().MouseWheel);

            // Drag event
            for (int i = 0; i < (int)ImGuiMouseButton.COUNT; i++)
            {
                var button = (ImGuiMouseButton)i;
                if (!IsMouseDragging(button) || !validBounds.IsInside(GetIO().MouseClickedPos[i]))
                {
                    lastDragDelta[i] = Vector2.Zero;
                    continue;
                }

                var delta = GetMouseDragDelta(button);
                OnDrag(button, delta - lastDragDelta[i]);
                lastDragDelta[i] = delta;
            }
        }
    }
}