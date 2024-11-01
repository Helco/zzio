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

        private Range rangePoints, rangeTraversableEdges;
        private bool
            showPoints = true,
            showTraversableEdges = true,
            hasPrecomputedVisibility;

        public WaypointComponent(ITagContainer diContainer)
        {
            diContainer.AddTag(this);
            editor = diContainer.GetTag<SceneEditor>();
            editor.fbArea.OnRender += HandleRender;
            editor.OnLoadScene += HandleLoadScene;
            editor.editor.AddInfoSection("Waypoints", HandleInfoSection, false);

            var menuBar = diContainer.GetTag<MenuBarWindowTag>();
            menuBar.AddCheckbox("View/Waypoints/Points", () => ref showPoints, () => editor.fbArea.IsDirty = true);
            menuBar.AddCheckbox("View/Waypoints/Traversable", () => ref showTraversableEdges, () => editor.fbArea.IsDirty = true);

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
            showTraversableEdges = true;
            rangePoints = rangeTraversableEdges = default;
            if (editor.scene == null)
                return;

            var wpSystem = editor.scene.waypointSystem;
            var idToIndex = wpSystem.Waypoints.Indexed().ToDictionary(t => t.Value.Id, t => t.Index);
            var walkableLinks = LinkSet(wpSystem.Waypoints.Select(wp => wp.WalkableIds));
            var jumpableLinks = LinkSet(wpSystem.Waypoints.Select(wp => wp.JumpableIds));
            hasPrecomputedVisibility = wpSystem.Waypoints.Any(wp => wp.VisibleIds?.Length > 0);

            rangePoints = 0..(wpSystem.Waypoints.Length * 3);
            rangeTraversableEdges = rangePoints.Suffix(walkableLinks.Count + jumpableLinks.Count);
            lineRenderer.Reserve(
                wpSystem.Waypoints.Length * 3 +
                walkableLinks.Count + jumpableLinks.Count);
            foreach (var wp in wpSystem.Waypoints)
                lineRenderer.AddCross(IColor.White, wp.Position, 0.1f);
            AddLinkSet(walkableLinks, new(0, 0, 230), new(0, 0, 130));
            AddLinkSet(jumpableLinks, new(230, 0, 0), new(130, 0, 0));

            void AddLinkSet(Dictionary<(int, int), bool> linkSet, IColor fullColor, IColor halfColor)
            {
                foreach (var ((linkFrom, linkTo), isFull) in linkSet)
                    lineRenderer.Add(
                        isFull ? fullColor : halfColor,
                        wpSystem.Waypoints[linkFrom].Position,
                        wpSystem.Waypoints[linkTo].Position);
            }

            Dictionary<(int, int), bool> LinkSet(IEnumerable<IEnumerable<uint>?> halfLinks)
            {
                if (!halfLinks.Any())
                    return [];
                var fullLinks = new Dictionary<(int, int), bool>((halfLinks.First()?.Count() ?? 1) * halfLinks.Count());
                foreach (var (halfLinkSet, i) in halfLinks.Indexed())
                {
                    if (halfLinkSet == null)
                        continue;
                    foreach (var jId in halfLinkSet)
                    {
                        var j = idToIndex[jId];
                        var key = i < j ? (i, j) : (j, i);
                        if (!fullLinks.TryAdd(key, false))
                            fullLinks[key] = true;
                    }
                }
                return fullLinks;
            }
        }
        
        private void HandleRender(CommandList cl)
        {
            if (lineRenderer.Count == 0)
                return;

            if (showPoints)
                lineRenderer.Render(cl, rangePoints);
            if (showTraversableEdges)
                lineRenderer.Render(cl, rangeTraversableEdges);
        }

        private void HandleInfoSection()
        {
            var wpSystem = editor.scene?.waypointSystem;
            LabelText("Version", wpSystem?.Version.ToString() ?? "n/a");
            LabelText("Waypoints", wpSystem?.Waypoints?.Length.ToString() ?? "");
            LabelText("Precomp. visibility", hasPrecomputedVisibility.ToString());
        }
    }
}
