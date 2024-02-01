using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using DefaultEcs.System;
using ImGuiNET;
using Veldrid;
using zzio;
using zzio.effect;
using zzio.effect.parts;
using zzio.vfs;
using zzre.imgui;
using zzre.materials;
using zzre.rendering;

namespace zzre.tools;

public partial class EffectEditor : ListDisposable, IDocumentEditor, IECSWindow
{
    private readonly ITagContainer diContainer;
    private readonly TwoColumnEditorTag editor;
    private readonly Camera camera;
    private readonly OrbitControlsTag controls;
    private readonly GraphicsDevice device;
    private readonly FramebufferArea fbArea;
    private readonly IResourcePool resourcePool;
    private readonly DebugLineRenderer gridRenderer;
    private readonly OpenFileModal openFileModal;
    private readonly GameTime gameTime;
    private readonly CachedAssetLoader<Texture> textureLoader;
    private readonly CachedAssetLoader<ClumpMesh> clumpLoader;
    private readonly EffectCombiner emptyEffect = new();
    private readonly DefaultEcs.World ecsWorld = new();
    private readonly SequentialSystem<float> updateSystems = new();
    private readonly SequentialSystem<CommandList> renderSystems = new();
    private ECSExplorer? ecsExplorer;
    private bool isPlaying = true;

    private EffectCombiner Effect => loadedEffect ?? emptyEffect;
    private EffectCombiner? loadedEffect = null;
    private DefaultEcs.Entity effectEntity;
    private DefaultEcs.Entity[] partEntities = Array.Empty<DefaultEcs.Entity>();

    public Window Window { get; }
    public IResource? CurrentResource { get; private set; }

    public EffectEditor(ITagContainer diContainer_)
    {
        diContainer = diContainer_.ExtendedWith(ecsWorld);
        AddDisposable(diContainer);
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

        diContainer.AddTag(new EffectMesh(diContainer, 1024, 2048));
        diContainer.AddTag(camera = new Camera(diContainer));
        controls = new OrbitControlsTag(Window, camera.Location, diContainer);
        AddDisposable(controls);

        gridRenderer = new DebugLineRenderer(diContainer);
        gridRenderer.Material.LinkTransformsTo(camera);
        gridRenderer.Material.World.Ref = Matrix4x4.Identity;
        gridRenderer.AddGrid();
        AddDisposable(gridRenderer);

        AddDisposable(textureLoader = new CachedAssetLoader<Texture>(new TextureAssetLoader(diContainer)));
        AddDisposable(clumpLoader = new CachedClumpMeshLoader(diContainer));
        diContainer.AddTag<IAssetLoader<Texture>>(textureLoader);
        diContainer.AddTag<IAssetLoader<ClumpMesh>>(clumpLoader);

        diContainer.AddTag(new game.resources.EffectMaterial(diContainer));

        updateSystems = new SequentialSystem<float>(
            new game.systems.effect.EffectCombiner(diContainer) { AddIndexAsComponent = true },
            new game.systems.effect.MovingPlanes(diContainer),
            new game.systems.effect.RandomPlanes(diContainer));
        AddDisposable(updateSystems);

        renderSystems = new SequentialSystem<CommandList>(
            new game.systems.effect.EffectRenderer(diContainer, game.components.RenderOrder.EarlyEffect),
            new game.systems.effect.EffectRenderer(diContainer, game.components.RenderOrder.Effect),
            new game.systems.effect.EffectRenderer(diContainer, game.components.RenderOrder.LateEffect));
        AddDisposable(renderSystems);

        editor.AddInfoSection("Info", HandleInfoContent);
        editor.AddInfoSection("Playback", HandlePlaybackContent);

        menuBar.AddButton("View/Show all parts", () => SetAllVisibilities(true));
        menuBar.AddButton("View/Hide all parts", () => SetAllVisibilities(false));

#if DEBUG
        menuBar.AddButton("Debug/ECS Explorer", HandleOpenECSExplorer);
#endif
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
        KillEffect();
        textureLoader.Clear();
        clumpLoader.Clear();

        using (var stream = resource.OpenContent())
        {
            if (stream == null)
                throw new FileNotFoundException($"Failed to open {resource.Path}");
            loadedEffect = new();
            loadedEffect.Read(stream);
        }

        SpawnEffect();
        isPlaying = true;

        editor.ClearInfoSections();
        editor.AddInfoSection("Info", HandleInfoContent);
        editor.AddInfoSection("Playback", HandlePlaybackContent);
        foreach (var (part, i) in Effect.parts.Indexed())
        {
            editor.AddInfoSection($"{part.Type} \"{part.Name}\"", part switch
            {
                MovingPlanes mp => () => HandlePart(mp),
                RandomPlanes rp => () => HandlePart(rp),
                ParticleEmitter pe => () => HandlePart(pe),
                BeamStar bs => () => HandlePart(bs),
                _ => () => { } // ignore for now
            }, defaultOpen: false, () => HandlePartPreContent(i));
        }

        controls.ResetView();
        controls.CameraAngle = new Vector2(45f, -45f) * MathF.PI / 180f;
        fbArea.IsDirty = true;
        CurrentResource = resource;
        Window.Title = $"Effect Editor - {resource.Path.ToPOSIXString()}";
    }

    private void KillEffect()
    {
        effectEntity.Dispose();
        foreach (var entity in partEntities)
            entity.Dispose();
        partEntities = Array.Empty<DefaultEcs.Entity>();
    }

    private void SpawnEffect()
    {
        effectEntity = ecsWorld.CreateEntity();
        effectEntity.Set(loadedEffect);
        ecsWorld.Publish(new game.messages.SpawnEffectCombiner(
            0, // we do not have the EffectCombiner resource manager, the value here does not matter
            AsEntity: effectEntity,
            Position: Vector3.Zero));
        partEntities = ecsWorld.GetEntities()
            .With<game.components.Parent>()
            .AsEnumerable()
            .OrderBy(e => e.Get<int>())
            .ToArray();
    }

    private void ResetEffect()
    {
        var visibilities = partEntities
            .Select(e => e.Get<game.components.Visibility>())
            .ToArray();
        KillEffect();
        SpawnEffect();
        foreach (var (entity, visibility) in partEntities.Zip(visibilities))
            entity.Set(visibility);
    }

    private void SetAllVisibilities(bool isVisible)
    {
        foreach (var entity in partEntities)
            entity.Set(isVisible ? game.components.Visibility.Visible : game.components.Visibility.Invisible);
    }

    private void HandleResize() => camera.Aspect = fbArea.Ratio;

    private void HandleRender(CommandList cl)
    {
        if (isPlaying && effectEntity.IsAlive)
            updateSystems.Update(gameTime.Delta);

        cl.PushDebugGroup(Window.Title);
        camera.Update(cl);
        gridRenderer.Render(cl);
        renderSystems.Update(cl);
        cl.PopDebugGroup();

        fbArea.IsDirty = true;
    }

    private void HandleInfoContent()
    {
        ImGui.InputText("Description", ref Effect.description, 512);
        ImGui.DragFloat3("Position", ref Effect.position);
        ImGui.DragFloat3("Forwards", ref Effect.forwards);
        ImGui.DragFloat3("Upwards", ref Effect.upwards);
    }

    private void HandlePlaybackContent()
    {
        static void UndoSlider(string label, ref float value, float min, float max, float defaultValue)
        {
            ImGui.SliderFloat(label, ref value, min, max);
            if (ImGui.IsItemClicked(ImGuiMouseButton.Right) || ImGui.IsItemClicked(ImGuiMouseButton.Middle))
                value = defaultValue;
        }
        game.components.effect.CombinerPlayback dummyPlayback = new();
        var optPlayback = effectEntity.TryGet<game.components.effect.CombinerPlayback>();
        ref var playback = ref (optPlayback.HasValue ? ref optPlayback.Value : ref dummyPlayback);

        if (!effectEntity.IsAlive)
            ImGui.BeginDisabled();

        ImGui.Checkbox("Looping", ref Effect.isLooping);
        var normalizedTime = playback.CurTime / Effect.Duration;
        if (playback.IsLooping)
            normalizedTime = normalizedTime - MathF.Truncate(normalizedTime);
        ImGui.ProgressBar(normalizedTime, new Vector2(0f, 0f), $"{playback.CurTime:F2} / {Effect.Duration}");
        ImGui.SameLine();
        ImGui.Text("Time");
        ImGui.SliderFloat("Progress", ref playback.CurProgress, 0f, 100f);
        UndoSlider("Length", ref playback.Length, 0f, 5f, 1f);

        if (ImGui.Button(IconFonts.ForkAwesome.FastBackward))
            ResetEffect();
        ImGui.SameLine();
        if (isPlaying && ImGui.Button(IconFonts.ForkAwesome.Pause))
            isPlaying = false;
        else if (!isPlaying && ImGui.Button(IconFonts.ForkAwesome.Play))
            isPlaying = true;

        if (!effectEntity.IsAlive)
            ImGui.EndDisabled();
    }

    private void HandleMenuOpen()
    {
        openFileModal.InitialSelectedResource = CurrentResource;
        openFileModal.Modal.Open();
    }

    private void HandlePartPreContent(int i)
    {
        var isVisible = partEntities[i].Get<game.components.Visibility>() == game.components.Visibility.Visible;
        var (on, off) = Effect.parts[i] is Sound // that is some unnecessary detail...
            ? (IconFonts.ForkAwesome.VolumeUp, IconFonts.ForkAwesome.VolumeOff)
            : (IconFonts.ForkAwesome.Eye, IconFonts.ForkAwesome.EyeSlash);
        if (ImGui.Button(isVisible ? on : off, new Vector2(24f, 0f)))
        {
            partEntities[i].Set(isVisible
                ? game.components.Visibility.Invisible
                : game.components.Visibility.Visible);
            fbArea.IsDirty = true;
        }
    }

    private void HandleOpenECSExplorer()
    {
        if (ecsExplorer == null)
        {
            ecsExplorer = new ECSExplorer(diContainer, this);
            ecsExplorer.Window.OnClose += () => ecsExplorer = null;
        }
        else
            diContainer.GetTag<WindowContainer>().SetNextFocusedWindow(ecsExplorer.Window);
    }

    public IEnumerable<(string, DefaultEcs.World)> GetWorlds()
    {
        yield return ("Effect", ecsWorld);
    }
}
