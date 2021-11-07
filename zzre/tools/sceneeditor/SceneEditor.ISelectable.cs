using ImGuizmoNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Veldrid;
using zzio.primitives;
using zzre.imgui;
using zzre.materials;
using zzre.rendering;

namespace zzre.tools
{
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

        private readonly List<IEnumerable<ISelectable>> selectableContainers = new List<IEnumerable<ISelectable>>();
        private IEnumerable<ISelectable> Selectables => selectableContainers.SelectMany(c => c);

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

        private class SelectionComponent : BaseDisposable
        {
            private const float MinViewDistance = 0.5f;

            private readonly SceneEditor editor;
            private readonly ITagContainer diContainer;
            private readonly Camera camera;
            private readonly DebugBoxLineRenderer boxBoundsRenderer;
            private readonly DebugDiamondSphereLineRenderer sphereBoundsRenderer;

            private IRenderable? activeBoundsRenderer = null;
            private ISelectable[] lastPotentials = new ISelectable[0];
            private int lastPotentialI;

            public SelectionComponent(ITagContainer diContainer)
            {
                diContainer.AddTag(this);
                this.diContainer = diContainer;
                editor = diContainer.GetTag<SceneEditor>();
                camera = diContainer.GetTag<Camera>();
                var fbArea = diContainer.GetTag<FramebufferArea>();
                var mouseEventArea = diContainer.GetTag<MouseEventArea>();

                boxBoundsRenderer = new DebugBoxLineRenderer(diContainer);
                boxBoundsRenderer.Material.LinkTransformsTo(camera);
                boxBoundsRenderer.Material.World.Value = Matrix4x4.Identity;
                boxBoundsRenderer.Color = IColor.Red;
                sphereBoundsRenderer = new DebugDiamondSphereLineRenderer(diContainer);
                sphereBoundsRenderer.Material.LinkTransformsTo(camera);
                sphereBoundsRenderer.Material.World.Value = Matrix4x4.Identity;
                sphereBoundsRenderer.Color = IColor.Red;

                editor.Window.OnContent += HandleGizmos;
                editor.OnLoadScene += () => editor.Selected = null;
                editor.OnNewSelection += HandleNewSelection;
                fbArea.OnRender += HandleRender;
                mouseEventArea.OnButtonUp += HandleClick;
            }

            protected override void DisposeManaged()
            {
                base.DisposeManaged();
                boxBoundsRenderer.Dispose();
                sphereBoundsRenderer.Dispose();
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
                    selected.Location.LocalToWorld = matrix;
                    editor.TriggerSelectionManipulate();
                    HandleNewSelection(selected); // to update the bounds
                }
            }

            private void HandleNewSelection(ISelectable? newSelected)
            {
                var bounds = newSelected?.RenderedBounds;
                if (bounds is OrientedBox)
                {
                    boxBoundsRenderer.Bounds = (OrientedBox)bounds;
                    activeBoundsRenderer = boxBoundsRenderer;
                }
                else if (bounds is Sphere)
                {
                    sphereBoundsRenderer.Bounds = (Sphere)bounds;
                    activeBoundsRenderer = sphereBoundsRenderer;
                }
                else
                    activeBoundsRenderer = null;
                editor.fbArea.IsDirty = true;
            }

            private void HandleRender(CommandList cl) => activeBoundsRenderer?.Render(cl);

            private void HandleClick(MouseButton button, Vector2 pos)
            {
                if (button != MouseButton.Left || ImGuizmo.IsOver() || ImGuizmo.IsUsing())
                    return;

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
}
