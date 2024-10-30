using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Veldrid;
using zzio;
using zzio.scn;
using zzre.debug;
using zzre.imgui;
using zzre.materials;
using zzre.rendering;
using static ImGuiNET.ImGui;
using static zzre.imgui.ImGuiEx;
using Quaternion = System.Numerics.Quaternion;

namespace zzre.tools;

public partial class SceneEditor
{
    private sealed class WaypointComponent : BaseDisposable
    {
        private enum EdgeVisibility
        {
            None,
            Traversable,
            Visibility
        }

        private readonly DebugLineRenderer lineRenderer;
        private readonly SceneEditor editor;

        private Range rangePoints, rangeTraversableEdges, rangeVisibleEdges;
        private bool showPoints = true;
        private EdgeVisibility edgeVisibility = EdgeVisibility.Traversable;

        public WaypointComponent(ITagContainer diContainer)
        {
            diContainer.AddTag(this);
            editor = diContainer.GetTag<SceneEditor>();
            editor.fbArea.OnRender += HandleRender;
            editor.OnLoadScene += HandleLoadScene;
            editor.editor.AddInfoSection("Waypoints", HandleInfoSection, false);

            var menuBar = diContainer.GetTag<MenuBarWindowTag>();
            menuBar.AddCheckbox("View/Waypoints/Points", () => ref showPoints, () => editor.fbArea.IsDirty = true);
            menuBar.AddRadio("View/Waypoints/Edges", () => ref edgeVisibility, () => editor.fbArea.IsDirty = true);

            lineRenderer = new(diContainer);
            lineRenderer.Material.LinkTransformsTo(diContainer.GetTag<Camera>());
            lineRenderer.Material.World.Ref = Matrix4x4.Identity;
            lineRenderer.Material.DepthTest = true;
        }

        protected override void DisposeManaged()
        {
            base.DisposeManaged();
            lineRenderer.Dispose();
        }

        private void HandleLoadScene()
        {
            lineRenderer.Clear();
            showPoints = true;
            edgeVisibility = EdgeVisibility.Traversable;
            if (editor.scene == null)
                return;

            var wpSystem = editor.scene.waypointSystem;
            rangePoints = 0..(wpSystem.waypointData.Length * 3);
            lineRenderer.Reserve(wpSystem.waypointData.Length * 3);
            foreach (var wp in wpSystem.waypointData)
                lineRenderer.AddCross(IColor.White, wp.v1, 0.1f);
        }
        
        private void HandleRender(CommandList cl)
        {
            if (lineRenderer.Count == 0)
                return;

            if (showPoints)
                lineRenderer.Render(cl, rangePoints);
            switch (edgeVisibility)
            {
                case EdgeVisibility.None: break;
                case EdgeVisibility.Traversable: lineRenderer.Render(cl, rangeTraversableEdges); break;
                case EdgeVisibility.Visibility: lineRenderer.Render(cl, rangeVisibleEdges); break;
                default: throw new NotImplementedException($"Unimplemented edge visibility mode: {edgeVisibility}");
            }
        }

        private void HandleInfoSection()
        {
            var wpSystem = editor.scene?.waypointSystem;
            LabelText("Waypoints", wpSystem?.waypointData?.Length.ToString() ?? "");
        }
    }
}
