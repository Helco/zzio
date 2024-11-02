using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using DefaultEcs.System;
using Silk.NET.Core.Native;
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
        private const float WaypointCrossSize = 0.1f;
        private const float WaypointSphereSize = 0.2f;

        private enum Selection
        {
            None,
            Waypoint,
            Group
        }

        private enum EdgeVisibility
        {
            None,
            Traversable,
            Visibility
        }

        private readonly DebugLineRenderer lineRenderer;
        private readonly SceneEditor editor;
        private readonly Dictionary<(int, int), bool> walkableLinks = new(), jumpableLinks = new();
        private readonly Dictionary<uint, int> idToIndex = new();
        private readonly Dictionary<uint, (IColor full, IColor half)> groupColors = new();

        private Range rangePoints, rangeTraversableEdges;
        private bool
            showPoints = true,
            showTraversableEdges = true,
            colorGroups = false,
            hasPrecomputedVisibility;
        private EdgeVisibility edgeVisibility = EdgeVisibility.Traversable; // for selected waypoints
        private Selection selection = Selection.None;
        private int selectedWaypoint = -1, selectedGroup = -1;

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
            selectedWaypoint = selectedGroup = -1;
            lineRenderer.Clear();
            walkableLinks.Clear();
            jumpableLinks.Clear();
            idToIndex.Clear();
            groupColors.Clear();
            showPoints = true;
            showTraversableEdges = true;
            rangePoints = rangeTraversableEdges = default;
            edgeVisibility = EdgeVisibility.Traversable;
            selection = Selection.None;
            if (editor.scene == null)
                return;

            var wpSystem = editor.scene.waypointSystem;
            idToIndex.EnsureCapacity(wpSystem.Waypoints.Length);
            foreach (var (wp, index) in wpSystem.Waypoints.Indexed())
                idToIndex.Add(wp.Id, index);
            LinkSet(wpSystem.Waypoints.Select(wp => wp.WalkableIds), walkableLinks);
            LinkSet(wpSystem.Waypoints.Select(wp => wp.JumpableIds), jumpableLinks);
            hasPrecomputedVisibility = wpSystem.Waypoints.Any(wp => wp.VisibleIds?.Length > 0);

            var groupIds = wpSystem.Waypoints.Select(wp => wp.Group).Distinct();
            var groupColors_ = groupIds.Zip(MathEx.GoldenRatioColors().Zip(MathEx.GoldenRatioColors(saturation: 0.5f)));
            foreach (var (groupId, colors) in groupColors_)
                groupColors[groupId] = colors;

            rangePoints = 0..(wpSystem.Waypoints.Length * 3);
            rangeTraversableEdges = rangePoints.Suffix(walkableLinks.Count + jumpableLinks.Count);
            UpdateUnselectedEdges();

            void LinkSet(IEnumerable<IEnumerable<uint>?> halfLinks, Dictionary<(int, int), bool> fullLinks)
            {
                if (!halfLinks.Any())
                    return;
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
            }
        }
        
        private void HandleRender(CommandList cl)
        {
            if (lineRenderer.Count == 0)
                return;

            if (selection == Selection.None)
            {
                if (showPoints)
                    lineRenderer.Render(cl, rangePoints);
                if (showTraversableEdges)
                    lineRenderer.Render(cl, rangeTraversableEdges);
            }
            else
                lineRenderer.Render(cl);
        }

        private void HandleInfoSection()
        {
            var wpSystem = editor.scene?.waypointSystem;
            var wpCount = wpSystem?.Waypoints?.Length ?? 0;
            LabelText("Version", wpSystem?.Version.ToString() ?? "n/a");
            LabelText("Waypoints", wpCount.ToString());
            LabelText("Groups", groupColors.Count.ToString());
            LabelText("Precomp. visibility", hasPrecomputedVisibility.ToString());
            NewLine();
            Text("Selection:");
            if (EnumRadioButtonGroup(ref selection))
            {
                selectedGroup = selectedWaypoint = -1;
                switch(selection)
                {
                    case Selection.None: UpdateUnselectedEdges(); break;
                    case Selection.Waypoint: UpdateSelectedWaypointEdges(); break;
                    case Selection.Group: UpdateSelectedGroupEdges(); break;
                    default: break;
                }
            }
            NewLine();
            switch(selection)
            {
                case Selection.None:
                    if (Checkbox("Color groups", ref colorGroups))
                        UpdateUnselectedEdges();
                    break;
                case Selection.Waypoint:
                    if (DragInt("Index", ref selectedWaypoint, 0.2f, -1, wpCount - 1) |
                        EnumCombo("Edges", ref edgeVisibility))
                        UpdateSelectedWaypointEdges();
                    break;
                case Selection.Group:
                    if (DragInt("Index", ref selectedGroup, 0.1f, -1, groupColors.Count - 1))
                        UpdateSelectedGroupEdges();
                    break;
                default: break;
            }
        }

        private void UpdateUnselectedEdges()
        {
            editor.fbArea.IsDirty = true;
            var wpSystem = editor.scene!.waypointSystem;
            lineRenderer.Clear();
            lineRenderer.Reserve(
                wpSystem.Waypoints.Length * 3 +
                walkableLinks.Count + jumpableLinks.Count);
            foreach (var wp in wpSystem.Waypoints)
            {
                var color = colorGroups ? groupColors[wp.Group].full : IColor.White;
                lineRenderer.AddCross(color, wp.Position, WaypointCrossSize);
            }
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
        }

        private void UpdateSelectedWaypointEdges()
        {
            editor.fbArea.IsDirty = true;
            lineRenderer.Clear();
            var wpSystem = editor.scene!.waypointSystem;
            var waypoint = selectedWaypoint < 0
                ? null as Waypoint?
                : wpSystem.Waypoints[selectedWaypoint];
            var selectedPosition = waypoint?.Position ?? Vector3.Zero;
            var allLinkedIds =
                (waypoint is null ? null
                : edgeVisibility is EdgeVisibility.Traversable ? waypoint.Value.WalkableIds.Concat(waypoint.Value.JumpableIds)
                : edgeVisibility is EdgeVisibility.Visibility ? waypoint.Value.VisibleIds
                : null)
                ?? Enumerable.Empty<uint>();
            for (int i = 0; i < wpSystem.Waypoints.Length; i++)
            {
                if (i == selectedWaypoint)
                {
                    lineRenderer.AddCross(IColor.Red, selectedPosition, WaypointCrossSize);
                    lineRenderer.AddDiamondSphere(new(selectedPosition, WaypointSphereSize), IColor.Red);
                }
                else
                {
                    var color = allLinkedIds.Contains(wpSystem.Waypoints[i].Id) ? IColor.Blue : IColor.Grey;
                    lineRenderer.AddCross(color, wpSystem.Waypoints[i].Position, WaypointCrossSize);
                }
            }

            switch (edgeVisibility)
            {
                case EdgeVisibility.Traversable:
                    AddLinkSet(waypoint?.WalkableIds, new(0, 0, 230));
                    AddLinkSet(waypoint?.JumpableIds, new(230, 0, 0));
                    break;
                case EdgeVisibility.Visibility:
                    AddLinkSet(waypoint?.VisibleIds, new(0, 230, 0));
                    break;
                default: break;
            }

            void AddLinkSet(uint[]? edges, IColor color)
            {
                foreach (var otherId in edges ?? [])
                {
                    var otherWaypoint = wpSystem.Waypoints[idToIndex[otherId]];
                    lineRenderer.Add(color, otherWaypoint.Position, selectedPosition);
                }
            }
        }

        private void UpdateSelectedGroupEdges()
        {
            editor.fbArea.IsDirty = true;
            lineRenderer.Clear();
            if (selectedGroup < 0)
                return;
            var wpSystem = editor.scene!.waypointSystem;
            AddGroup((uint)selectedGroup, true);
            if (!wpSystem.CompatibleGroups.TryGetValue((uint)selectedGroup, out var compatibleGroups))
                return;
            foreach (var compatibleGroup in compatibleGroups)
                AddGroup(compatibleGroup, false);
            
            void AddGroup(uint groupId, bool fullColor)
            {
                var color = fullColor ? groupColors[groupId].full : groupColors[groupId].half;
                foreach (ref readonly var waypoint in wpSystem.Waypoints.AsSpan())
                {
                    if (waypoint.Group != groupId)
                        continue;
                    lineRenderer.AddCross(color, waypoint.Position, WaypointCrossSize);
                }
            }
        }
    }
}
