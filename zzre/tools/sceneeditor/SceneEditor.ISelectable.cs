using ImGuizmoNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using zzio;
using zzre.imgui;
using zzre.materials;
using zzre.rendering;

namespace zzre.tools;

public partial class SceneEditor
{
    public interface ISelectable
    {
        string Title { get; }
        Location Location { get; }
        IRaycastable SelectableBounds { get; }
        IRaycastable? RenderedBounds { get; }
        float ViewSize { get; }
    }

    private readonly List<IEnumerable<ISelectable>> selectableContainers = [];
    private IEnumerable<ISelectable> Selectables => selectableContainers.SelectMany(c => c);

    private static bool dragMode;
    private ISelectable? _selected;
    private ISelectable? Selected
    {
        get => _selected;
        set
        {
            _selected = value;
            OnNewSelection.Invoke(value);
        }
    }

    private event Action<ISelectable?> OnNewSelection = _ => { };
    private event Action<ISelectable> OnSelectionManipulate = _ => { };

    private void MoveCameraToSelected() =>
        localDiContainer.GetTag<SelectionComponent>().MoveCameraToSelected();

    private void TriggerSelectionManipulate()
    {
        if (Selected == null)
            throw new InvalidOperationException("Cannot trigger selection manipulate without a selection");
        OnSelectionManipulate(Selected);
    }

    private sealed class SelectionComponent : BaseDisposable
    {
        private const float MinViewDistance = 0.5f;

        private readonly SceneEditor editor;
        private readonly Camera camera;
        private readonly DebugLineRenderer boundsRenderer;

        private ISelectable[] lastPotentials = [];
        private int lastPotentialI;

        public SelectionComponent(ITagContainer diContainer)
        {
            diContainer.AddTag(this);
            editor = diContainer.GetTag<SceneEditor>();
            camera = diContainer.GetTag<Camera>();
            var fbArea = diContainer.GetTag<FramebufferArea>();
            var mouseEventArea = diContainer.GetTag<MouseEventArea>();

            boundsRenderer = new DebugLineRenderer(diContainer);
            boundsRenderer.Material.LinkTransformsTo(camera);
            boundsRenderer.Material.World.Value = Matrix4x4.Identity;

            editor.Window.OnContent += HandleGizmos;
            editor.OnLoadScene += () => editor.Selected = null;
            editor.OnNewSelection += HandleNewSelection;
            fbArea.OnRender += boundsRenderer.Render;
            mouseEventArea.OnButtonUp += HandleClick;
        }

        protected override void DisposeManaged()
        {
            base.DisposeManaged();
            boundsRenderer.Dispose();
        }

        private void HandleGizmos()
        {
            var selected = editor.Selected;
            if (selected == null)
                return;

            var view = camera.Location.WorldToLocal;
            var projection = camera.Projection;
            var matrix = selected.Location.LocalToWorld;
            ImGuizmo.SetDrawlist();
            if (ImGuizmo.Manipulate(ref view.M11, ref projection.M11, OPERATION.TRANSLATE, MODE.LOCAL, ref matrix.M11))
            {
                dragMode = true;
                selected.Location.LocalToWorld = matrix;
                editor.TriggerSelectionManipulate();
                HandleNewSelection(selected); // to update the bounds
            }
        }

        private void HandleNewSelection(ISelectable? newSelected)
        {
            var bounds = newSelected?.RenderedBounds;
            boundsRenderer.Clear();
            if (bounds is OrientedBox orientedBoxBounds)
                boundsRenderer.AddBox(orientedBoxBounds, IColor.Red);
            else if (bounds is Box boxBounds)
                boundsRenderer.AddBox(boxBounds, IColor.Red);
            else if (bounds is Sphere sphereBounds)
                boundsRenderer.AddDiamondSphere(sphereBounds, IColor.Red);
            editor.fbArea.IsDirty = true;
        }

        private void HandleClick(MouseButton button, Vector2 pos)
        {
            if (button != MouseButton.Left)// || ImGuizmo.IsOver() || ImGuizmo.IsUsing())
                return;

            if (dragMode)
            {
                dragMode = false;
                return;
            }

            var ray = camera.RayAt((pos * 2f - Vector2.One) * new Vector2(1f, -1f));
            var newPotentials = editor.Selectables
                .Select(s => (obj: s, rayCast: s.SelectableBounds.Cast(ray)))
                .Where(t => t.rayCast.HasValue)
                .OrderBy(t => t.rayCast?.Distance)
                .Select(t => t.obj)
                .ToArray();
            if (!newPotentials.Any())
                return;

            ISelectable nextSelected;
            if (newPotentials.SequenceEqual(lastPotentials))
            {
                lastPotentialI = (lastPotentialI + 1) % lastPotentials.Length;
                nextSelected = lastPotentials[lastPotentialI];
            }
            else
            {
                lastPotentials = newPotentials;
                lastPotentialI = 0;
                nextSelected = newPotentials.First();
            }
            editor.Selected = nextSelected;
        }

        public void MoveCameraToSelected()
        {
            if (editor.Selected == null)
                return;
            var selected = editor.Selected;
            var camera = editor.camera;
            var distance = Math.Max(MinViewDistance, Math.Abs(selected.ViewSize / MathF.Sin(camera.VFoV / 2f)));
            camera.Location.LocalPosition = selected.Location.GlobalPosition + camera.Location.GlobalForward * distance;
        }
    }
}
