using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using Veldrid;
using zzio;
using zzio.vfs;
using zzre.core;
using zzre.imgui;
using zzre.materials;
using zzre.rendering;
using static ImGuiNET.ImGui;

namespace zzre.tools
{
    public partial class ActorEditor : ListDisposable, IDocumentEditor
    {
        private readonly ITagContainer diContainer;
        private readonly ITagContainer localDiContainer;
        private readonly TwoColumnEditorTag editor;
        private readonly OrbitControlsTag controls;
        private readonly GraphicsDevice device;
        private readonly FramebufferArea fbArea;
        private readonly IResourcePool resourcePool;
        private readonly DebugGridRenderer gridRenderer;
        private readonly OpenFileModal openFileModal;

        private ActorExDescription? description;
        private Part? body;
        private Part? wings;
        private Location actorLocation = new Location();
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
            editor = new TwoColumnEditorTag(Window, diContainer);
            controls = new OrbitControlsTag(Window, diContainer);
            var onceAction = new OnceAction();
            Window.AddTag(onceAction);
            Window.OnContent += onceAction.Invoke;
            var menuBar = new MenuBarWindowTag(Window);
            menuBar.AddItem("Open", HandleMenuOpen);
            fbArea = Window.GetTag<FramebufferArea>();
            fbArea.OnRender += HandleRender;
            diContainer.GetTag<OpenDocumentSet>().AddEditor(this);

            openFileModal = new OpenFileModal(diContainer);
            openFileModal.Filter = "*.aed";
            openFileModal.IsFilterChangeable = false;
            openFileModal.OnOpenedResource += LoadActor;

            locationBuffer = new LocationBuffer(device.ResourceFactory);
            AddDisposable(locationBuffer);
            localDiContainer = diContainer.ExtendedWith(locationBuffer);

            gridRenderer = new DebugGridRenderer(diContainer);
            gridRenderer.Material.LinkTransformsTo(controls.Projection, controls.View, controls.World);
            AddDisposable(gridRenderer);
            localDiContainer.AddTag<IStandardTransformMaterial>(gridRenderer.Material);

            editor.AddInfoSection("Info", HandleInfoContent);
            editor.AddInfoSection("Animation Playback", HandlePlaybackContent);
            editor.AddInfoSection("Body animations", () => HandlePartContent(false, () => body?.AnimationsContent() ?? false), false);
            editor.AddInfoSection("Wings animations", () => HandlePartContent(true, () => wings?.AnimationsContent() ?? false), false);
            editor.AddInfoSection("Body skeleton", () => HandlePartContent(false, () => body?.skeletonRenderer.Content() ?? false), false);
            editor.AddInfoSection("Wings skeleton", () => HandlePartContent(true, () => wings?.skeletonRenderer.Content() ?? false), false);
        }

        public static ActorEditor OpenFor(ITagContainer diContainer, string pathText)
        {
            var resourcePool = diContainer.GetTag<IResourcePool>();
            var resource = resourcePool.FindFile(pathText);
            if (resource == null)
                throw new FileNotFoundException($"Could not find model at {pathText}");
            return OpenFor(diContainer, resource);
        }

        public static ActorEditor OpenFor(ITagContainer diContainer, IResource resource)
        {
            var openDocumentSet = diContainer.GetTag<OpenDocumentSet>();
            if (openDocumentSet.TryGetEditorFor(resource, out var prevEditor))
            {
                prevEditor.Window.Focus();
                return (ActorEditor)prevEditor;
            }
            var newEditor = new ActorEditor(diContainer);
            newEditor.LoadActor(resource);
            return newEditor;
        }

        public void LoadActor(string pathText)
        {
            var resource = resourcePool.FindFile(pathText);
            if (resource == null)
                throw new FileNotFoundException($"Could not find actor at {pathText}");
            LoadActor(resource);
        }

        public void LoadActor(IResource resource) =>
            Window.GetTag<OnceAction>().Next += () => LoadActorNow(resource);

        private void LoadActorNow(IResource resource)
        {
            if (resource.Equals(CurrentResource))
                return;
            CurrentResource = null;

            using var contentStream = resource.OpenContent();
            if (contentStream == null)
                throw new IOException($"Could not open actor at {resource.Path.ToPOSIXString()}");
            description = ActorExDescription.ReadNew(contentStream);

            body = new Part(localDiContainer, description.body.model, description.body.animations);
            body.location.Parent = actorLocation;
            wings = null;
            if (description.HasWings)
            {
                wings = new Part(localDiContainer, description.wings.model, description.wings.animations);
                wings.location.Parent = body.skeleton.Bones[description.attachWingsToBone];
            }

            controls.ResetView();
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
            void ModelLink(string label, string? modelName)
            {
                bool isEnabled = modelName != null && modelName.Length > 0;
                if (ImGuiEx.Hyperlink(label, isEnabled ? modelName! : "<none>", true, isEnabled))
                    ModelViewer.OpenFor(diContainer, "resources/models/actorsex/" + modelName);
            }

            ModelLink("Body: ", description?.body.model);
            Text($"Body animations: {description?.body.animations.Length ?? 0}");
            Text($"Body bones: {body?.skeleton.Bones.Count ?? 0}");
            
            NewLine();
            ModelLink("Wings: ", description?.wings.model);
            Text($"Wings animations: {description?.wings.animations.Length ?? 0}");
            Text($"Wings bones: {wings?.skeleton.Bones.Count ?? 0}");

            NewLine();
            void BoneLink(string label, int? boneIdx)
            {
                if (ImGuiEx.Hyperlink(label, boneIdx?.ToString() ?? "<none>", false, boneIdx != null))
                {
                    body?.skeletonRenderer.HighlightBone(boneIdx!.Value);
                    fbArea.IsDirty = true;
                    if (body != null && body.skeletonRenderer.RenderMode == DebugSkeletonRenderMode.Invisible)
                        body.skeletonRenderer.RenderMode = DebugSkeletonRenderMode.Bones;
                }
            }
            BoneLink("Head bone: ", description?.headBoneID);
            BoneLink("Effect bone: ", description?.effectBoneID);
            BoneLink("Wing attach bone: ", description?.attachWingsToBone);
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
