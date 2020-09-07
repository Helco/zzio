﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
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
            Bounds Bounds { get; } // In object space
            Location Location { get; }
        }

        private List<IEnumerable<ISelectable>> selectableContainers = new List<IEnumerable<ISelectable>>();
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

        private void MoveCameraToSelected() =>
            localDiContainer.GetTag<SelectionComponent>().MoveCameraToSelected();

        private class SelectionComponent : BaseDisposable
        {
            private const float MinViewDistance = 0.5f;

            private readonly SceneEditor editor;
            private readonly ITagContainer diContainer;
            private readonly LocationBuffer locationBuffer;
            private readonly DebugBoundsLineRenderer boundsRenderer;

            private DeviceBufferRange? selectedBounds;

            public SelectionComponent(ITagContainer diContainer)
            {
                diContainer.AddTag(this);
                this.diContainer = diContainer;
                editor = diContainer.GetTag<SceneEditor>();
                locationBuffer = diContainer.GetTag<LocationBuffer>();
                var camera = diContainer.GetTag<Camera>();
                var fbArea = diContainer.GetTag<FramebufferArea>();

                boundsRenderer = new DebugBoundsLineRenderer(diContainer);
                boundsRenderer.Material.LinkTransformsTo(camera);
                boundsRenderer.Color = IColor.Red;
                editor.OnLoadScene += () => editor.Selected = null;
                editor.OnNewSelection += HandleNewSelection;
                fbArea.OnRender += HandleRender;
            }

            protected override void DisposeManaged()
            {
                base.DisposeManaged();
                boundsRenderer.Dispose();
                if (selectedBounds != null)
                    locationBuffer.Remove(selectedBounds.Value);
            }

            private void HandleNewSelection(ISelectable? newSelected)
            {
                if (selectedBounds != null)
                    locationBuffer.Remove(selectedBounds.Value);
                if (newSelected == null)
                    return;
                selectedBounds = locationBuffer.Add(newSelected.Location);
                boundsRenderer.Bounds = newSelected.Bounds;
                boundsRenderer.Material.World.BufferRange = selectedBounds.Value;
                editor.fbArea.IsDirty = true;
            }

            private void HandleRender(CommandList cl)
            {
                if (selectedBounds == null)
                    return;
                boundsRenderer.Render(cl);
            }

            public void MoveCameraToSelected()
            {
                if (editor.Selected == null)
                    return;
                var selected = editor.Selected;
                var camera = editor.camera;
                var size = selected.Bounds.Size;
                var maxSize = Math.Max(Math.Max(size.X, size.Y), size.Z);
                var distance = Math.Max(MinViewDistance, Math.Abs(maxSize / MathF.Sin(camera.VFoV / 2f)));
                camera.Location.LocalPosition =
                    Vector3.Transform(selected.Bounds.Center, selected.Location.LocalToWorld) -
                    camera.Location.GlobalForward * distance;
            }
        }
    }
}