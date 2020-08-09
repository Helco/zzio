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
    public partial class ActorEditor : ListDisposable
    {
        private readonly ITagContainer diContainer;
        private readonly SimpleEditorTag editor;
        private readonly GraphicsDevice device;
        private readonly FramebufferArea fbArea;
        private readonly IResourcePool resourcePool;
        private readonly DebugGridRenderer gridRenderer;
        private readonly OpenFileModal openFileModal;

        private ActorExDescription? description;
        private Part? body;
        private Part? wings;

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
            editor.AddInfoSection("Body Skeleton", HandleBodySkeletonContent, false);
            editor.AddInfoSection("Wings Skeleton", HandleWingsSkeletonContent, false);
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

            using var contentStream = resource.OpenContent();
            if (contentStream == null)
                throw new IOException($"Could not open actor at {resource.Path.ToPOSIXString()}");
            description = ActorExDescription.ReadNew(contentStream);

            body = new Part(this, description.body.model);
            wings = description.HasWings || false ? new Part(this, description.wings.model) : null;
        }

        private void HandleRender(CommandList cl)
        {
            gridRenderer.Render(cl);
            body?.Render(cl);
            wings?.Render(cl);

            body?.RenderDebug(cl);
            wings?.RenderDebug(cl);
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
            Text($"Head bone index: {NoneIfEmptyOrNull(description?.headBoneID.ToString())}");
            Text($"Effect bone index: {NoneIfEmptyOrNull(description?.effectBoneID.ToString())}");
            Text($"Attach bone index: {NoneIfEmptyOrNull(description?.attachWingsToBone.ToString())}");
        }

        private void HandleBodySkeletonContent()
        {
            if (body == null)
                Text("No actor is loaded.");
            else if (body.skeletonRenderer.Content())
                fbArea.IsDirty = true;
        }

        private void HandleWingsSkeletonContent()
        {
            if (body == null)
                Text("No actor is loaded.");
            else if (wings == null)
                Text("This actor has no wings.");
            else if (wings.skeletonRenderer.Content())
                fbArea.IsDirty = true;
        }
    }
}
