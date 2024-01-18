﻿using System;
using System.IO;
using System.Numerics;
using Veldrid;
using zzio;
using zzio.vfs;
using zzre.imgui;
using zzre.materials;
using zzre.rendering;
using static ImGuiNET.ImGui;

namespace zzre.tools;

public partial class ActorEditor : ListDisposable, IDocumentEditor
{
    private enum HeadIKMode
    {
        Disabled = 0,
        Enabled,
        Frozen
    }

    private readonly ITagContainer diContainer;
    private readonly ITagContainer localDiContainer;
    private readonly TwoColumnEditorTag editor;
    private readonly Camera camera;
    private readonly OrbitControlsTag controls;
    private readonly GraphicsDevice device;
    private readonly FramebufferArea fbArea;
    private readonly IResourcePool resourcePool;
    private readonly DebugLineRenderer gridRenderer;
    private readonly OpenFileModal openFileModal;

    private ActorExDescription? description;
    private Part? body;
    private Part? wings;
    private readonly Location actorLocation = new();
    private readonly LocationBuffer locationBuffer;
    private HeadIKMode headIKMode = HeadIKMode.Disabled;

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
        var onceAction = new OnceAction();
        Window.AddTag(onceAction);
        Window.OnContent += onceAction.Invoke;
        var menuBar = new MenuBarWindowTag(Window);
        menuBar.AddButton("Open", HandleMenuOpen);
        fbArea = Window.GetTag<FramebufferArea>();
        fbArea.OnResize += HandleResize;
        fbArea.OnRender += HandleRender;
        diContainer.GetTag<OpenDocumentSet>().AddEditor(this);

        openFileModal = new OpenFileModal(diContainer)
        {
            Filter = "*.aed",
            IsFilterChangeable = false
        };
        openFileModal.OnOpenedResource += Load;

        locationBuffer = new LocationBuffer(device);
        AddDisposable(locationBuffer);
        localDiContainer = diContainer.ExtendedWith(locationBuffer);
        camera = new Camera(localDiContainer);
        AddDisposable(camera);
        controls = new OrbitControlsTag(Window, camera.Location, localDiContainer);
        AddDisposable(controls);
        localDiContainer.AddTag(camera);

        gridRenderer = new DebugLineRenderer(diContainer);
        gridRenderer.Material.LinkTransformsTo(camera);
        gridRenderer.Material.World.Ref = Matrix4x4.Identity;
        gridRenderer.AddGrid();
        AddDisposable(gridRenderer);
        localDiContainer.AddTag<IStandardTransformMaterial>(gridRenderer.Material);

        editor.AddInfoSection("Info", HandleInfoContent);
        editor.AddInfoSection("Animation Playback", HandlePlaybackContent);
        editor.AddInfoSection("Body animations", () => HandlePartContent(false, () => body?.AnimationsContent() ?? false), false);
        editor.AddInfoSection("Wings animations", () => HandlePartContent(true, () => wings?.AnimationsContent() ?? false), false);
        editor.AddInfoSection("Body skeleton", () => HandlePartContent(false, () => body?.skeletonRenderer.Content() ?? false), false);
        editor.AddInfoSection("Wings skeleton", () => HandlePartContent(true, () => wings?.skeletonRenderer.Content() ?? false), false);
        editor.AddInfoSection("Head IK", HandleHeadIKContent, false);
    }

    public void Load(string pathText)
    {
        var resource = resourcePool.FindFile(pathText);
        if (resource == null)
            throw new FileNotFoundException($"Could not find actor at {pathText}");
        Load(resource);
    }

    public void Load(IResource resource) =>
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

    private void HandleResize() => camera.Aspect = fbArea.Ratio;

    private void HandleRender(CommandList cl)
    {
        camera.Update(cl);
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
                diContainer.GetTag<OpenDocumentSet>().OpenWith<ModelViewer>("resources/models/actorsex/" + modelName);
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

    private void HandleHeadIKContent()
    {
        if (description == null || body == null)
            return;
        if (description.headBoneID < 0)
        {
            Text("This actor has no head.");
            return;
        }

        ImGuiEx.EnumRadioButtonGroup(ref headIKMode);
        var newValue = (description.headBoneID, camera.Location.GlobalPosition);
        body.singleIK = headIKMode switch
        {
            HeadIKMode.Disabled => null,
            HeadIKMode.Enabled => newValue,
            HeadIKMode.Frozen => body.singleIK ?? newValue,
            _ => throw new NotImplementedException("Unimplemented head IK mode")
        };
    }
}
