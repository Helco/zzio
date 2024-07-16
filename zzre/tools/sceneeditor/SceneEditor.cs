using System;
using System.IO;
using System.Numerics;
using Veldrid;
using zzio.scn;
using zzio.vfs;
using zzre.imgui;
using zzre.materials;
using zzre.rendering;

using KeyCode = Silk.NET.SDL.KeyCode;

namespace zzre.tools;

public partial class SceneEditor : ListDisposable, IDocumentEditor
{
    private readonly TwoColumnEditorTag editor;
    private readonly FlyControlsTag controls;
    private readonly FramebufferArea fbArea;
    private readonly IResourcePool resourcePool;
    private readonly OpenFileModal openFileModal;
    private readonly LocationBuffer locationBuffer;
    private readonly DebugLineRenderer gridRenderer;
    private readonly Camera camera;
    private readonly AssetLocalRegistry assetRegistry;
    private readonly DefaultEcs.World ecsWorld;

    private readonly TriggerComponent triggerComponent;
    private readonly FOModelComponent foModelComponent;

    private event Action OnLoadScene = () => { };

    private readonly ITagContainer localDiContainer;
    private Scene? scene;

    public IResource? CurrentResource { get; private set; }
    public Window Window { get; }

    private bool ControlIsPressed;

    public SceneEditor(ITagContainer diContainer)
    {
        resourcePool = diContainer.GetTag<IResourcePool>();
        Window = diContainer.GetTag<WindowContainer>().NewWindow("Scene Editor");
        Window.AddTag(this);
        Window.InitialBounds = new Rect(float.NaN, float.NaN, 1100.0f, 600.0f);
        editor = new TwoColumnEditorTag(Window, diContainer);
        var onceAction = new OnceAction();
        Window.AddTag(onceAction);
        Window.OnContent += onceAction.Invoke;

        locationBuffer = new LocationBuffer(diContainer.GetTag<GraphicsDevice>());
        AddDisposable(locationBuffer);
        var menuBar = new MenuBarWindowTag(Window);
        menuBar.AddButton("Open", HandleMenuOpen);
        menuBar.AddButton("Save", SaveScene);
        menuBar.AddButton("Duplicate Selection", DuplicateCurrentSelection);
        menuBar.AddButton("Delete Selection", DeleteCurrentSelection);
        openFileModal = new OpenFileModal(diContainer)
        {
            Filter = "*.scn",
            IsFilterChangeable = false
        };
        openFileModal.OnOpenedResource += Load;

        camera = new Camera(diContainer.ExtendedWith(locationBuffer));
        AddDisposable(camera);
        controls = new FlyControlsTag(Window, camera.Location, diContainer);
        gridRenderer = new DebugLineRenderer(diContainer);
        gridRenderer.Material.LinkTransformsTo(camera);
        gridRenderer.Material.World.Ref = Matrix4x4.Identity;
        gridRenderer.AddGrid();
        AddDisposable(gridRenderer);
        fbArea = Window.GetTag<FramebufferArea>();
        fbArea.OnResize += HandleResize;
        fbArea.OnRender += camera.Update;
        fbArea.OnRender += locationBuffer.Update;
        fbArea.OnRender += gridRenderer.Render;

        localDiContainer = diContainer
           .FallbackTo(Window)
           .ExtendedWith(this, Window, gridRenderer, locationBuffer);
        AddDisposable(localDiContainer);
        localDiContainer
            .AddTag<IAssetRegistry>(assetRegistry = new AssetLocalRegistry("SceneEditor", localDiContainer))
            .AddTag(ecsWorld = new DefaultEcs.World())
            .AddTag(camera);
        AssetRegistry.SubscribeAt(localDiContainer.GetTag<DefaultEcs.World>());
        assetRegistry.DelayDisposals = false;
        new MiscComponent(localDiContainer);
        new DatasetComponent(localDiContainer);
        new WorldComponent(localDiContainer);
        new ModelComponent(localDiContainer);
        new LightComponent(localDiContainer);
        new EffectComponent(localDiContainer);
        new Sample3DComponent(localDiContainer);
        new SelectionComponent(localDiContainer);
        foModelComponent = new FOModelComponent(localDiContainer);
        triggerComponent = new TriggerComponent(localDiContainer);
        Window.OnKeyUp += HandleKeyUp;
        Window.OnKeyDown += HandleKeyDown;
        Window.OnContent += HandleOnContent;
        diContainer.GetTag<OpenDocumentSet>().AddEditor(this);
    }

    protected override void DisposeManaged()
    {
        localDiContainer.RemoveTag<IAssetRegistry>(dispose: false);
        base.DisposeManaged();
        assetRegistry.Dispose();
    }

    private void HandleKeyDown(KeyCode key)
    {
        if (key == KeyCode.KLctrl)
            ControlIsPressed = true;
    }
    private void HandleKeyUp(KeyCode key)
    {
        if (key == KeyCode.KLctrl)
            ControlIsPressed = false;
        else if (ControlIsPressed)
        {
            if (key == KeyCode.KD)
                DuplicateCurrentSelection();
            else if (key == KeyCode.KS)
                SaveScene();
            else if (key == KeyCode.KX)
                DeleteCurrentSelection();
        }
    }
    private void HandleOnContent()
    {
        if (Window.IsFocused == false)
        {
            ControlIsPressed = false;
        }
    }
    private void DuplicateCurrentSelection()
    {
        triggerComponent.DuplicateCurrentTrigger();
        foModelComponent.DuplicateCurrentFoModel();

    }
    private void DeleteCurrentSelection()
    {
        triggerComponent.DeleteCurrentTrigger();
        foModelComponent.DeleteCurrentFoModel();
    }
    public void Load(string pathText)
    {
        var resource = resourcePool.FindFile(pathText) ?? throw new FileNotFoundException($"Could not find world at {pathText}");
        Load(resource);
    }

    public void Load(IResource resource) =>
        Window.GetTag<OnceAction>().Next += () => LoadSceneNow(resource);

    private void LoadSceneNow(IResource resource)
    {
        if (resource.Equals(CurrentResource))
            return;
        CurrentResource = null;

        using var contentStream = resource.OpenContent() ?? throw new IOException($"Could not open scene at {resource.Path.ToPOSIXString()}");
        scene = new Scene();
        scene.Read(contentStream);

        CurrentResource = resource;
        controls.ResetView();
        fbArea.IsDirty = true;
        Window.Title = $"Scene Editor - {resource.Path.ToPOSIXString()}";
        ecsWorld.Publish(new game.messages.SceneLoaded(scene, Savegame: null!));
        OnLoadScene();
        assetRegistry.ApplyAssets();
        editor.ResetColumnWidth();
    }

    private void HandleResize() => camera.Aspect = fbArea.Ratio;

    private void HandleMenuOpen()
    {
        openFileModal.InitialSelectedResource = CurrentResource;
        openFileModal.Modal.Open();
    }
    private void SaveScene()
    {
        if (CurrentResource == null || scene == null)
            return;
        triggerComponent.SyncWithScene();
        foModelComponent.SyncWithScene();
        var path = Path.Combine(Environment.CurrentDirectory, "..", CurrentResource.Path.ToString());

        var stream = new FileStream(path, FileMode.Create);
        scene.Write(stream);
    }

}
