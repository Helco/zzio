using System;
using System.Collections.Generic;
using System.Text;
using Veldrid;
using zzio.utils;
using zzre.imgui;
using zzre.materials;
using zzre.rendering;

namespace zzre.tools
{
    public partial class SceneEditor
    {
        private class WorldComponent : BaseDisposable
        {
            private readonly ITagContainer diContainer;
            private readonly SceneEditor editor;
            private readonly WorldRenderer renderer;
            private readonly FlyControlsTag controls;
            private WorldBuffers? buffers;
            private bool isVisible = true;

            public WorldComponent(ITagContainer diContainer)
            {
                diContainer.AddTag(this);
                this.diContainer = diContainer;
                var window = diContainer.GetTag<Window>();
                var fbArea = window.GetTag<FramebufferArea>();
                fbArea.OnRender += HandleRender;
                controls = window.GetTag<FlyControlsTag>();
                renderer = new WorldRenderer(diContainer);
                window.GetTag<MenuBarWindowTag>().AddCheckbox("View/World", () => ref isVisible, () => fbArea.IsDirty = true);
                editor = diContainer.GetTag<SceneEditor>();
                editor.OnLoadScene += HandleLoadScene;
            }

            protected override void DisposeManaged()
            {
                base.DisposeManaged();
                buffers?.Dispose();
                renderer?.Dispose();
            }

            private void HandleLoadScene()
            {
                buffers?.Dispose();
                buffers = null;
                if (editor.scene == null)
                    return;

                var fullPath = new FilePath("resources").Combine(editor.scene.misc.worldPath, editor.scene.misc.worldFile + ".bsp");
                buffers = new WorldBuffers(diContainer, fullPath);
                renderer.WorldBuffers = buffers;
                controls.Position = buffers.Origin;
            }

            private void HandleRender(CommandList cl)
            {
                if (!isVisible)
                    return;

                renderer.ViewFrustumCulling.SetViewProjection(controls.View.Value, controls.Projection.Value);
                renderer.UpdateVisibility();
                renderer.Render(cl);
            }
        }
    }
}
