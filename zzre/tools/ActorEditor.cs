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
        private LocationBuffer locationBuffer;

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
            gridRenderer.Material.LinkTransformsTo(editor.Projection, editor.View, editor.World);
            AddDisposable(gridRenderer);

            locationBuffer = new LocationBuffer(device.ResourceFactory);
            AddDisposable(locationBuffer);

            editor.AddInfoSection("Info", HandleInfoContent);
            editor.AddInfoSection("Animation Playback", HandlePlaybackContent);
            editor.AddInfoSection("Body animations", () => HandlePartContent(false, () => body?.AnimationsContent() ?? false), false);
            editor.AddInfoSection("Wings animations", () => HandlePartContent(true, () => wings?.AnimationsContent() ?? false), false);
            editor.AddInfoSection("Body skeleton", () => HandlePartContent(false, () => body?.skeletonRenderer.Content() ?? false), false);
            editor.AddInfoSection("Wings skeleton", () => HandlePartContent(true, () => wings?.skeletonRenderer.Content() ?? false), false);
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

            body = new Part(this, description.body.model, description.body.animations);
            wings = null;
            if (description.HasWings)
            {
                wings = new Part(this, description.wings.model, description.wings.animations);
                wings.location.Parent = body.skeleton.Bones[description.attachWingsToBone];
            }

            editor.ResetView();
            fbArea.IsDirty = true;
            CurrentResource = resource;
        }

        private void HandleRender(CommandList cl)
        {
            locationBuffer.Update(cl);
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
            Text($"Body animations: {description?.body.animations.Length ?? 0}");
            Text($"Body bones: {body?.skeleton.Bones.Count ?? 0}");
            
            NewLine();
            Text($"Wings: {NoneIfEmptyOrNull(description?.wings.model)}");
            Text($"Wings animations: {description?.wings.animations.Length ?? 0}");
            Text($"Wings bones: {wings?.skeleton.Bones.Count ?? 0}");

            // TODO: Add buttons to highlight bone
            NewLine();
            Text($"Head bone index: {NoneIfEmptyOrNull(description?.headBoneID.ToString())}");
            Text($"Effect bone index: {NoneIfEmptyOrNull(description?.effectBoneID.ToString())}");
            Text($"Attach bone index: {NoneIfEmptyOrNull(description?.attachWingsToBone.ToString())}");
        }

        private void HandlePlaybackContent()
        {
            if (body != null)
            {
                Text("Body:");
                fbArea.IsDirty |= body.PlaybackContent();
            }
            if (wings != null)
            {
                NewLine();
                Text("Wings:");
                fbArea.IsDirty |= wings.PlaybackContent();
            }
        }

        private void HandlePartContent(bool isWingsAction, Func<bool> action)
        {
            if (body == null)
                Text("No actor is loaded.");
            else if (isWingsAction && wings == null)
                Text("This actor has no wings.");
            else if (action())
                fbArea.IsDirty = true;
        }
    }
}
