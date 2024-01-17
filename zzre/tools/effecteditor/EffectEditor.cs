using ImGuiNET;
using System;
using System.IO;
using System.Linq;
using System.Numerics;
using Veldrid;
using zzio;
using zzio.effect;
using zzio.effect.parts;
using zzio.vfs;
using zzre.imgui;
using zzre.materials;
using zzre.rendering;
using zzre.rendering.effectparts;

namespace zzre.tools;

public partial class EffectEditor : ListDisposable, IDocumentEditor
{
    private readonly ITagContainer diContainer;
    private readonly TwoColumnEditorTag editor;
    private readonly Camera camera;
    private readonly OrbitControlsTag controls;
    private readonly GraphicsDevice device;
    private readonly FramebufferArea fbArea;
    private readonly IResourcePool resourcePool;
    private readonly DebugGridRenderer gridRenderer;
    private readonly OpenFileModal openFileModal;
    private readonly LocationBuffer locationBuffer;
    private readonly GameTime gameTime;
    private readonly CachedAssetLoader<Texture> textureLoader;
    private readonly CachedAssetLoader<ClumpMesh> clumpLoader;

    private EffectCombinerRenderer? effectRenderer;
    private EffectCombiner Effect => effectRenderer?.Effect ?? emptyEffect;
    private EffectCombiner emptyEffect = new();
    private bool[] isVisible = Array.Empty<bool>();
    private bool isPlaying = false;
    private float timeScale = 1f, progressSpeed = 0f;

    public Window Window { get; }
    public IResource? CurrentResource { get; private set; }

    public EffectEditor(ITagContainer diContainer)
    {
        device = diContainer.GetTag<GraphicsDevice>();
        resourcePool = diContainer.GetTag<IResourcePool>();
        gameTime = diContainer.GetTag<GameTime>();
        Window = diContainer.GetTag<WindowContainer>().NewWindow("Effect Editor");
        Window.InitialBounds = new Rect(float.NaN, float.NaN, 1100f, 600f);
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
            Filter = "*.ed",
            IsFilterChangeable = false
        };
        openFileModal.OnOpenedResource += Load;

        locationBuffer = new LocationBuffer(device);
        this.diContainer = diContainer.ExtendedWith(locationBuffer);
        AddDisposable(this.diContainer);
        this.diContainer.AddTag(camera = new Camera(this.diContainer));
        this.diContainer.AddTag<IQuadMeshBuffer<EffectVertex>>(new DynamicQuadMeshBuffer<EffectVertex>(device.ResourceFactory, 1024));
        this.diContainer.AddTag<IQuadMeshBuffer<SparkVertex>>(new DynamicQuadMeshBuffer<SparkVertex>(device.ResourceFactory, 256));
        controls = new OrbitControlsTag(Window, camera.Location, this.diContainer);
        AddDisposable(controls);
        gridRenderer = new DebugGridRenderer(this.diContainer);
        gridRenderer.Material.LinkTransformsTo(camera);
        gridRenderer.Material.World.Ref = Matrix4x4.Identity;
        AddDisposable(gridRenderer);

        AddDisposable(textureLoader = new CachedAssetLoader<Texture>(new TextureAssetLoader(diContainer)));
        AddDisposable(clumpLoader = new CachedClumpMeshLoader(diContainer));
        this.diContainer.AddTag<IAssetLoader<Texture>>(textureLoader);
        this.diContainer.AddTag<IAssetLoader<ClumpMesh>>(clumpLoader);

        editor.AddInfoSection("Info", HandleInfoContent);
        editor.AddInfoSection("Playback", HandlePlaybackContent);
    }

    protected override void DisposeManaged()
    {
        base.DisposeManaged();
        effectRenderer?.Dispose();
    }

    public void Load(string pathText)
    {
        var resource = resourcePool.FindFile(pathText);
        if (resource == null)
            throw new FileNotFoundException($"Could not find model at {pathText}");
        Load(resource);
    }

    public void Load(IResource resource) =>
        Window.GetTag<OnceAction>().Next += () => LoadEffectNow(resource);

    private void LoadEffectNow(IResource resource)
    {
        if (resource.Equals(CurrentResource))
            return;
        CurrentResource = null;
        emptyEffect = new EffectCombiner();

        effectRenderer?.Dispose();
        effectRenderer = new EffectCombinerRenderer(diContainer, resource);
        effectRenderer.Location.LocalPosition = Vector3.Zero;
        effectRenderer.Location.LocalRotation = Quaternion.Identity;

        editor.ClearInfoSections();
        editor.AddInfoSection("Info", HandleInfoContent);
        editor.AddInfoSection("Playback", HandlePlaybackContent);
        foreach (var (partRenderer, i) in effectRenderer.Parts.Indexed())
        {
            var part = Effect.parts[i];
            editor.AddInfoSection($"{part.Type} \"{part.Name}\"", part switch
            {
                MovingPlanes mp => () => HandlePart(mp, (MovingPlanesRenderer)partRenderer),
                RandomPlanes rp => () => HandlePart(rp, (RandomPlanesRenderer)partRenderer),
                ParticleEmitter pe => () => HandlePart(pe, (ParticleEmitterRenderer)partRenderer),
                BeamStar bs => () => HandlePart(bs, (BeamStarRenderer)partRenderer),
                _ => () => { } // ignore for now
            }, defaultOpen: false, () => HandlePartPreContent(i));
        }
        isVisible = Enumerable.Repeat(true, effectRenderer.Parts.Count).ToArray();
        isPlaying = true;
        timeScale = 1f;
        progressSpeed = 0f;

        controls.ResetView();
        controls.CameraAngle = new Vector2(45f, -45f) * MathF.PI / 180f;
        fbArea.IsDirty = true;
        CurrentResource = resource;
        Window.Title = $"Effect Editor - {resource.Path.ToPOSIXString()}";
    }

    private void HandleResize() => camera.Aspect = fbArea.Ratio;

    private void HandleRender(CommandList cl)
    {
        locationBuffer.Update(cl);
        camera.Update(cl);
        gridRenderer.Render(cl);

        if (effectRenderer == null)
            return;
        foreach (var part in effectRenderer.Parts.Where((p, i) => isVisible[i]))
            part.Render(cl);
    }

    private void HandleInfoContent()
    {
        ImGui.InputText("Description", ref Effect.description, 512);

        var pos = effectRenderer?.Location.LocalPosition ?? Vector3.Zero;
        var forwards = Effect.forwards;
        var upwards = Effect.upwards;
        if (ImGui.DragFloat3("Position", ref pos) && effectRenderer != null)
            effectRenderer.Location.LocalPosition = pos;
        ImGui.DragFloat3("Forwards", ref forwards);
        ImGui.DragFloat3("Upwards", ref upwards);
    }

    private void HandlePlaybackContent()
    {
        static void UndoSlider(string label, ref float value, float min, float max, float defaultValue)
        {
            ImGui.SliderFloat(label, ref value, min, max);
            if (ImGui.IsItemClicked(ImGuiMouseButton.Right) || ImGui.IsItemClicked(ImGuiMouseButton.Middle))
                value = defaultValue;
        }

        ImGui.Checkbox("Looping", ref Effect.isLooping);

        float curTime = effectRenderer?.CurTime ?? 0f;
        ImGui.SliderFloat("Time", ref curTime, 0f, Effect.Duration, $"%.3f / {Effect.Duration}", ImGuiSliderFlags.NoInput);
        UndoSlider("Time Scale", ref timeScale, 0f, 5f, 1f);
        float progress = effectRenderer?.CurProgress ?? 0f;
        if (ImGui.SliderFloat("Progress", ref progress, 0f, 100f))
        {
            effectRenderer?.AddTime(0f, progress);
            fbArea.IsDirty = true;
        }
        UndoSlider("Progress Speed", ref progressSpeed, -2f, 2f, 0f);

        float length = effectRenderer?.Length ?? 0f;
        UndoSlider("Length", ref length, 0f, 5f, 1f);
        if (effectRenderer != null)
            effectRenderer.Length = length;

        if (ImGui.Button(IconFonts.ForkAwesome.FastBackward))
        {
            effectRenderer?.Reset();
            fbArea.IsDirty = true;
        }
        ImGui.SameLine();
        if (isPlaying && ImGui.Button(IconFonts.ForkAwesome.Pause))
            isPlaying = false;
        else if (!isPlaying && ImGui.Button(IconFonts.ForkAwesome.Play) && effectRenderer != null)
            isPlaying = true;

        if (isPlaying && effectRenderer != null)
        {
            var newProgress = effectRenderer.CurProgress + progressSpeed * 100f * gameTime.Delta;
            newProgress = Effect.isLooping
                ? newProgress < 0f ? 100f - newProgress
                : newProgress > 100f ? newProgress - 100f
                : newProgress
                : Math.Clamp(newProgress, 0f, 100f);
            effectRenderer.AddTime(gameTime.Delta * timeScale, newProgress);
            if (effectRenderer.IsDone)
                isPlaying = false;
            fbArea.IsDirty = true;
        }
    }

    private void HandleMenuOpen()
    {
        openFileModal.InitialSelectedResource = CurrentResource;
        openFileModal.Modal.Open();
    }

    private void HandlePartPreContent(int i)
    {
        if (ImGui.Button(isVisible[i] ? IconFonts.ForkAwesome.Eye : IconFonts.ForkAwesome.EyeSlash))
        {
            isVisible[i] = !isVisible[i];
            fbArea.IsDirty = true;
        }
    }
}
