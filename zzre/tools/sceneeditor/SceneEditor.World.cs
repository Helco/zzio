using Veldrid;
using zzio;
using zzre.imgui;
using zzre.rendering;

namespace zzre.tools;

public partial class SceneEditor
{
    private class WorldComponent : BaseDisposable
    {
        private readonly ITagContainer diContainer;
        private readonly SceneEditor editor;
        private readonly WorldRenderer renderer;
        private WorldBuffers? buffers;
        private bool isVisible = true;

        public WorldComponent(ITagContainer diContainer)
        {
            diContainer.AddTag(this);
            this.diContainer = diContainer;
            var fbArea = diContainer.GetTag<FramebufferArea>();
            fbArea.OnRender += HandleRender;
            renderer = new WorldRenderer(diContainer);
            diContainer.GetTag<MenuBarWindowTag>().AddCheckbox("View/World", () => ref isVisible, () => fbArea.IsDirty = true);
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

            var fullPath = new zzio.FilePath("resources").Combine(editor.scene.misc.worldPath, editor.scene.misc.worldFile + ".bsp");
            buffers = new WorldBuffers(diContainer, fullPath);
            renderer.WorldBuffers = buffers;
            editor.camera.Location.LocalPosition = -buffers.Origin;
        }

        private void HandleRender(CommandList cl)
        {
            if (!isVisible)
                return;

            renderer.UpdateVisibility();
            renderer.Render(cl);
        }
    }
}
