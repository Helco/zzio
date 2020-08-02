using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using Veldrid;

namespace zzre.imgui
{
    public class FramebufferWindowTag : FramebufferArea
    {
        public Window Window { get; }

        public FramebufferWindowTag(Window parent, GraphicsDevice device) : base(parent, device)
        {
            Window = parent;
            Window.AddTag(this);
            Window.OnBeforeContent += HandleBeforeContent;
            Window.OnContent += HandleContent;
        }

        private void HandleBeforeContent()
        {
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        }

        private void HandleContent()
        {
            Content();
            ImGui.PopStyleVar(1);
        }
    }
}
