using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using Veldrid;
using zzio.scn;
using zzio.vfs;
using zzre.core;
using zzre.imgui;
using zzre.materials;
using zzre.rendering;

namespace zzre.tools
{
    public partial class SceneEditor : ListDisposable, IDocumentEditor
    {
        private readonly ITagContainer diContainer;
        private readonly TwoColumnEditorTag editor;
        private readonly FlyControlsTag controls;
        private readonly FramebufferArea fbArea;
        private readonly IResourcePool resourcePool;
        private readonly OpenFileModal openFileModal;
        private readonly LocationBuffer locationBuffer;
        private readonly DebugGridRenderer gridRenderer;

        private event Action OnLoadedScene = () => { };

        private ITagContainer localDiContainer;
        private Scene? scene;

        public IResource? CurrentResource { get; private set; }
        public Window Window { get; }

        public SceneEditor(ITagContainer diContainer)
        {
            this.diContainer = diContainer;
            resourcePool = diContainer.GetTag<IResourcePool>();
            Window = diContainer.GetTag<WindowContainer>().NewWindow("Scene Editor");
            Window.AddTag(this);
            Window.InitialBounds = new Rect(float.NaN, float.NaN, 1100.0f, 600.0f);
            editor = new TwoColumnEditorTag(Window, diContainer);
            controls = new FlyControlsTag(Window, diContainer);
            var onceAction = new OnceAction();
            Window.AddTag(onceAction);
            Window.OnContent += onceAction.Invoke;
            gridRenderer = new DebugGridRenderer(diContainer);
            gridRenderer.Material.LinkTransformsTo(controls.Projection, controls.View, controls.World);
            AddDisposable(gridRenderer);
            var menuBar = new MenuBarWindowTag(Window);
            menuBar.AddItem("Open", HandleMenuOpen);
            fbArea = Window.GetTag<FramebufferArea>();
            fbArea.OnRender += gridRenderer.Render;
            openFileModal = new OpenFileModal(diContainer);
            openFileModal.Filter = "*.scn";
            openFileModal.IsFilterChangeable = false;
            openFileModal.OnOpenedResource += Load;
            locationBuffer = new LocationBuffer(diContainer.GetTag<GraphicsDevice>());

            localDiContainer = diContainer.ExtendedWith(this, Window, gridRenderer, locationBuffer);
        }

        public void Load(string pathText)
        {
            var resource = resourcePool.FindFile(pathText);
            if (resource == null)
                throw new FileNotFoundException($"Could not find world at {pathText}");
            Load(resource);
        }

        public void Load(IResource resource) =>
            Window.GetTag<OnceAction>().Next += () => LoadSceneNow(resource);

        private void LoadSceneNow(IResource resource)
        {
            if (resource.Equals(CurrentResource))
                return;
            CurrentResource = null;

            using var contentStream = resource.OpenContent();
            if (contentStream == null)
                throw new IOException($"Could not open scene at {resource.Path.ToPOSIXString()}");
            scene = new Scene();
            scene.Read(contentStream);

            CurrentResource = resource;
            controls.ResetView();
            fbArea.IsDirty = true;
            Window.Title = $"Scene Editor - {resource.Path.ToPOSIXString()}";
            OnLoadedScene();
        }

        private void HandleMenuOpen()
        {
            openFileModal.InitialSelectedResource = CurrentResource;
            openFileModal.Modal.Open();
        }
    }
}
