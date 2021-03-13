﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Veldrid;
using zzio.rwbs;
using zzre.core;
using zzio.utils;
using System.Numerics;
using ImGuiNET;
using zzio.vfs;
using zzre.rendering;
using zzre.materials;
using zzio.primitives;
using SixLabors.ImageSharp.ColorSpaces;
using SixLabors.ImageSharp;

namespace zzre.imgui
{
    /// <summary>
    /// A simple editor/viewer layout with an info section and
    /// a framebuffer area with orbit controls
    /// </summary>
    public class TwoColumnEditorTag
    {
        private readonly GraphicsDevice device;
        private readonly MouseEventArea mouseArea;
        private readonly FramebufferArea fbArea;

        private int didSetColumnWidth = 0;
        private List<(string name, Action content, bool defaultOpen)> infoSections = new List<(string, Action, bool)>();

        public Window Window { get; }
        

        public TwoColumnEditorTag(Window window, ITagContainer diContainer)
        {
            device = diContainer.GetTag<GraphicsDevice>();
            Window = window;
            Window.AddTag(this);
            Window.OnContent += HandleContent;
            fbArea = new FramebufferArea(Window, device);
            mouseArea = new MouseEventArea(Window);            
        }

        public void AddInfoSection(string name, Action content, bool defaultOpen = true) =>
            infoSections.Add((name, content, defaultOpen));

        public void ClearInfoSections() => infoSections.Clear();

        private void HandleContent()
        {
            ImGui.Columns(2, null, true);
            if (didSetColumnWidth < 2) // Why not, ImGui is weird
            {
                ImGui.SetColumnWidth(0, Window.InitialBounds.Size.X * 0.3f);
                didSetColumnWidth++;
            }
            ImGui.BeginChild("LeftColumn", ImGui.GetContentRegionAvail(), false, ImGuiWindowFlags.HorizontalScrollbar);
            foreach (var (name, content, isDefaultOpen) in infoSections)
            {
                var flags = isDefaultOpen ? ImGuiTreeNodeFlags.DefaultOpen : 0;
                if (!ImGui.CollapsingHeader(name, flags))
                    continue;

                ImGui.BeginGroup();
                ImGui.Indent();
                content();
                ImGui.EndGroup();

            }
            ImGui.EndChild();

            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, Vector2.Zero);
            ImGui.NextColumn();
            mouseArea.Content();
            fbArea.Content();
            ImGui.PopStyleVar(1);
        }
    }
}
