using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Veldrid;
using zzio.vfs;
using zzre.imgui;

namespace zzre.tools
{
    public class WorldViewer : ListDisposable, IDocumentEditor
    {
        private readonly ITagContainer diContainer;
        private readonly TwoColumnEditorTag editor;
        private readonly OrbitControlsTag controls;
        private readonly FramebufferArea fbArea;
        private readonly IResourcePool resourcePool;
        private readonly OpenFileModal openFileModal;

        public IResource? CurrentResource { get; private set; }
        public Window Window { get; }

        public WorldViewer(ITagContainer diContainer)
        {
            this.diContainer = diContainer;
            resourcePool = diContainer.GetTag<IResourcePool>();
            Window = diContainer.GetTag<WindowContainer>().NewWindow("World Viewer");
            Window.AddTag(this);
            Window.InitialBounds = new Rect(float.NaN, float.NaN, 1100.0f, 600.0f);
            editor = new TwoColumnEditorTag(Window, diContainer);
            controls = new OrbitControlsTag(Window, diContainer);
            new DeferredCallerTag(Window);
            var menuBar = new MenuBarWindowTag(Window);
            menuBar.AddItem("Open", HandleMenuOpen);
            fbArea = Window.GetTag<FramebufferArea>();
            fbArea.OnRender += HandleRender;
            diContainer.GetTag<OpenDocumentSet>().AddEditor(this);

            openFileModal = new OpenFileModal(diContainer);
            openFileModal.Filter = "*.bsp";
            openFileModal.IsFilterChangeable = false;
            openFileModal.OnOpenedResource += LoadWorld;

            editor.AddInfoSection("Statistics", HandleStatisticsContent);
            editor.AddInfoSection("Materials", HandleMaterialsContent);
            editor.AddInfoSection("BSP collision", HandleBSPCollisionContent);
        }

        public static WorldViewer OpenFor(ITagContainer diContainer, string pathText)
        {
            var resourcePool = diContainer.GetTag<IResourcePool>();
            var resource = resourcePool.FindFile(pathText);
            if (resource == null)
                throw new FileNotFoundException($"Could not find world at {pathText}");
            return OpenFor(diContainer, resource);
        }

        public static WorldViewer OpenFor(ITagContainer diContainer, IResource resource)
        {
            var openDocumentSet = diContainer.GetTag<OpenDocumentSet>();
            if (openDocumentSet.TryGetEditorFor(resource, out var prevEditor))
            {
                prevEditor.Window.Focus();
                return (WorldViewer)prevEditor;
            }
            var newEditor = new WorldViewer(diContainer);
            newEditor.LoadWorld(resource);
            return newEditor;
        }

        public void LoadWorld(string pathText)
        {
            var resource = resourcePool.FindFile(pathText);
            if (resource == null)
                throw new FileNotFoundException($"Could not find world at {pathText}");
            LoadWorld(resource);
        }

        public void LoadWorld(IResource resource) =>
            Window.GetTag<DeferredCallerTag>().Next += () => LoadWorldNow(resource);

        private void LoadWorldNow(IResource resource)
        {
            if (resource.Equals(CurrentResource))
                return;
            CurrentResource = null;

            // TODO: Add BSP world loading

            CurrentResource = resource;
            controls.ResetView();
            fbArea.IsDirty = true;
            Window.Title = $"World Viewer - {resource.Path.ToPOSIXString()}";
        }

        private void HandleRender(CommandList cl)
        {
            // TODO: Add BSP world rendering
        }

        private void HandleMenuOpen()
        {
            openFileModal.InitialSelectedResource = CurrentResource;
            openFileModal.Modal.Open();
        }

        private void HandleStatisticsContent()
        {
            // TODO: Add WorldViewer statistics info section
        }

        private void HandleMaterialsContent()
        {
            // TODO: Add WorldViewer materials info section
        }

        private void HandleBSPCollisionContent()
        {
            // TODO: Add WorldViewer BSP collision info section
        }
    }
}
