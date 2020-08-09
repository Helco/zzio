using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Veldrid;
using zzio;
using zzio.vfs;
using zzre.imgui;
using zzre.materials;
using zzre.rendering;
using static ImGuiNET.ImGui;

namespace zzre.tools
{
    public class ActorEditor : ListDisposable
    {
        private readonly ITagContainer diContainer;
        private readonly SimpleEditorTag editor;
        private readonly GraphicsDevice device;
        private readonly FramebufferArea fbArea;
        private readonly IResourcePool resourcePool;
        private readonly DebugGridRenderer gridRenderer;
        private readonly OpenFileModal openFileModal;

        private ActorExDescription? description;

        public Window Window { get; }
        public IResource? CurrentResource { get; private set; }

        public ActorEditor(ITagContainer diContainer)
        {
            this.diContainer = diContainer;
            device = diContainer.GetTag<GraphicsDevice>();
            resourcePool = diContainer.GetTag<IResourcePool>();
            Window = diContainer.GetTag<WindowContainer>().NewWindow("Actor Editor");
            Window.InitialBounds = new Rect(float.NaN, float.NaN, 1100.0f, 600.0f);
            Window.AddTag(this);
            editor = new SimpleEditorTag(Window, diContainer);
            new DeferredCallerTag(Window);
            var menuBar = new MenuBarWindowTag(Window);
            menuBar.AddItem("Open", HandleMenuOpen);
            fbArea = Window.GetTag<FramebufferArea>();
            fbArea.OnRender += HandleRender;

            openFileModal = new OpenFileModal(diContainer);
            openFileModal.Filter = "*.aed";
            openFileModal.IsFilterChangeable = false;
            openFileModal.OnOpenedResource += LoadActor;

            gridRenderer = new DebugGridRenderer(diContainer);
            gridRenderer.Material.Transformation.Buffer = editor.Transform.Buffer;
            AddDisposable(gridRenderer);

            editor.AddInfoSection("Info", HandleInfoContent);
        }

        public void LoadActor(string pathText)
        {
            var resource = resourcePool.FindFile(pathText);
            if (resource == null)
                throw new FileNotFoundException($"Could not find actor at {pathText}");
            LoadActor(resource);
        }

        public void LoadActor(IResource resource) =>
            Window.GetTag<DeferredCallerTag>().Next += () => LoadActorNow(resource);

        private void LoadActorNow(IResource resource)
        {
            if (resource.Equals(CurrentResource))
                return;
            CurrentResource = null;

        }

        private void HandleRender(CommandList cl)
        {
            gridRenderer.Render(cl);
        }

        private void HandleMenuOpen()
        {
            openFileModal.InitialSelectedResource = CurrentResource;
            openFileModal.Modal.Open();
        }

        private void HandleInfoContent()
        {
            string NoneIfEmptyOrNull(string? s) =>
                (s == null || s.Length <= 0) ? "<none>" : s;

            // TODO: Add buttons to open model viewer
            Text($"Body: {NoneIfEmptyOrNull(description?.body.model)}");
            Text($"Wings: {NoneIfEmptyOrNull(description?.wings.model)}");
            Text($"Body animations: {description?.body.animations.Length ?? 0}");
            Text($"Wings animations: {description?.wings.animations.Length ?? 0}");

            // TODO: Add buttons to highlight bone
            Text($"Body bone ID: {NoneIfEmptyOrNull(description?.headBoneID.ToString())}");
            Text($"Effect bone ID: {NoneIfEmptyOrNull(description?.effectBoneID.ToString())}");
            Text($"Attach bone ID: {NoneIfEmptyOrNull(description?.attachWingsToBone.ToString())}");
        }
    }
}
