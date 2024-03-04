using Veldrid;
using zzio;
using zzre.game.systems;
using zzre.imgui;
using zzre.rendering;

namespace zzre.tools;

public partial class SceneEditor
{
    private sealed class WorldComponent : BaseDisposable
    {
        private readonly SceneEditor editor;
        private readonly WorldRendererSystem worldRenderer;
        private readonly DefaultEcs.World ecsWorld;
        private bool isVisible = true;

        public WorldComponent(ITagContainer diContainer)
        {
            diContainer.AddTag(this);
            var fbArea = diContainer.GetTag<FramebufferArea>();
            fbArea.OnRender += HandleRender;
            ecsWorld = diContainer.GetTag<DefaultEcs.World>();
            worldRenderer = new(diContainer);
            diContainer.GetTag<MenuBarWindowTag>().AddCheckbox("View/World", () => ref isVisible, () => fbArea.IsDirty = true);
            editor = diContainer.GetTag<SceneEditor>();
            editor.OnLoadScene += HandleLoadScene;
        }

        protected override void DisposeManaged()
        {
            base.DisposeManaged();
            worldRenderer.Dispose();
        }

        private void HandleLoadScene()
        {
            if (editor.scene == null)
                return;

            editor.camera.Location.LocalPosition = -ecsWorld.Get<WorldMesh>().Origin;
        }

        private void HandleRender(CommandList cl)
        {
            if (!isVisible)
                return;

            worldRenderer.Update(cl);
        }
    }
}
