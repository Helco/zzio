using System;
using System.IO;
using System.Numerics;
using Veldrid;
using zzio.scn;
using zzio.vfs;
using zzre.imgui;
using zzre.materials;
using zzre.rendering;

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

    private event Action OnLoadScene = () => { };

    private readonly ITagContainer localDiContainer;
    private Scene? scene;

    public IResource? CurrentResource { get; private set; }
    public Window Window { get; }

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
            .ExtendedWith(this, Window, gridRenderer, locationBuffer)
            .AddTag<IAssetLoader<Texture>>(new CachedAssetLoader<Texture>(diContainer.GetTag<IAssetLoader<Texture>>()))
            .AddTag<IAssetLoader<ClumpMesh>>(new CachedClumpMeshLoader(diContainer))
            .AddTag(camera);
        new MiscComponent(localDiContainer);
        new DatasetComponent(localDiContainer);
        new WorldComponent(localDiContainer);
        new ModelComponent(localDiContainer);
        new FOModelComponent(localDiContainer);
        new TriggerComponent(localDiContainer);
        new LightComponent(localDiContainer);
        new EffectComponent(localDiContainer);
        new Sample3DComponent(localDiContainer);
        new SelectionComponent(localDiContainer);
        diContainer.GetTag<OpenDocumentSet>().AddEditor(this);
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
        localDiContainer.GetTag<IAssetLoader<Texture>>().Clear();
        localDiContainer.GetTag<IAssetLoader<ClumpMesh>>().Clear();

        using var contentStream = resource.OpenContent() ?? throw new IOException($"Could not open scene at {resource.Path.ToPOSIXString()}");
        scene = new Scene();
        scene.Read(contentStream);

        CurrentResource = resource;
        controls.ResetView();
        fbArea.IsDirty = true;
        Window.Title = $"Scene Editor - {resource.Path.ToPOSIXString()}";
        OnLoadScene();
    }

    private void HandleResize() => camera.Aspect = fbArea.Ratio;

    private void HandleMenuOpen()
    {
        openFileModal.InitialSelectedResource = CurrentResource;
        openFileModal.Modal.Open();
    }
}
