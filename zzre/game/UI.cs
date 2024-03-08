using System.Collections.Generic;
using System.Numerics;
using Veldrid;
using zzio;

namespace zzre.game;

public class UI : BaseDisposable, ITagContainer
{
    private readonly ITagContainer tagContainer;
    private readonly IZanzarahContainer zzContainer;
    private readonly AssetLocalRegistry assetRegistry;
    private readonly Remotery profiler;
    private readonly GameTime time;
    private readonly systems.RecordingSequentialSystem<float> updateSystems;
    private readonly systems.RecordingSequentialSystem<CommandList> renderSystems;
    private readonly GraphicsDevice graphicsDevice;

    public DeviceBuffer ProjectionBuffer { get; }
    public Rect LogicalScreen { get; set; }
    public DefaultEcs.Entity CursorEntity { get; }
    public UIBuilder Builder { get; }
    public DefaultEcs.World World { get; }

    public UI(ITagContainer diContainer)
    {
        tagContainer = new ExtendedTagContainer(diContainer);
        zzContainer = GetTag<IZanzarahContainer>();
        zzContainer.OnResize += HandleResize;
        profiler = GetTag<Remotery>();
        time = GetTag<GameTime>();

        var resourceFactory = GetTag<ResourceFactory>();
        graphicsDevice = GetTag<GraphicsDevice>();
        ProjectionBuffer = resourceFactory.CreateBuffer(
            // Buffer has to be multiple of 16 bytes big even though we only need 8
            new BufferDescription(sizeof(float) * 4, BufferUsage.UniformBuffer));
        ProjectionBuffer.Name = "UI Projection";
        HandleResize();

        AddTag(this);
        AddTag<IAssetRegistry>(assetRegistry = new AssetLocalRegistry("UI", tagContainer));
        AddTag(World = new DefaultEcs.World());
        AddTag(Builder = new UIBuilder(this));

        if (TryGetTag(out tools.AssetRegistryList assetRegistryList))
            assetRegistryList.Register("UI", assetRegistry);
        AssetRegistry.SubscribeAt(World);
        assetRegistry.DelayDisposals = true;
        // we still use DelayDisposal in UI to prevent mostly sound samples to be freed

        CursorEntity = World.CreateEntity();
        CursorEntity.Set<Rect>();
        CursorEntity.Set<components.Visibility>();

        updateSystems = new systems.RecordingSequentialSystem<float>(this);
        updateSystems.Add(
            // Sound (very early to make the context current)
            new systems.SoundContext(this),
            new systems.SoundListener(this),
            new systems.SoundEmitter(this),
            new systems.SoundFade(this),
            new systems.SoundStoppedEmitter(this),

            new systems.ui.Cursor(this),
            
            // Screens
            new systems.ui.ScrDeck(this),
            new systems.ui.ScrRuneMenu(this),
            new systems.ui.ScrBookMenu(this),
            new systems.ui.ScrMapMenu(this),
            new systems.ui.ScrGotCard(this),
            new systems.ui.ScrNotification(this),

            // UI elements
            new systems.ui.ButtonTiles(this),
            new systems.ui.Slider(this),
            new systems.ui.AnimatedLabel(this),
            new systems.ui.Label(this),
            new systems.ui.Tooltip(this),
            new systems.ui.CorrectRenderOrder(this),
            new systems.ui.Fade(this),

            new systems.Reaper(this),
            new systems.ParentReaper(this));

        renderSystems = new systems.RecordingSequentialSystem<CommandList>(this);
        renderSystems.Add(
            new systems.ui.Batcher(this));
    }

    protected override void DisposeManaged()
    {
        assetRegistry.DelayDisposals = false;
        tagContainer.RemoveTag<IAssetRegistry>(dispose: false);
        updateSystems.Dispose();
        renderSystems.Dispose();
        tagContainer.Dispose();
        assetRegistry.Dispose();
        ProjectionBuffer.Dispose();
        zzContainer.OnResize -= HandleResize;
    }

    public void Publish<T>() => World.Publish(default(T));
    public void Publish<T>(in T message) => World.Publish(message);

    public void Update()
    {
        using var _ = profiler.SampleCPU("UI.Update");
        assetRegistry.ApplyAssets();
        updateSystems.Update(time.Delta);
    }

    public void Render(CommandList cl)
    {
        using var _ = profiler.SampleCPU("UI.Render");
        assetRegistry.ApplyAssets();
        renderSystems.Update(cl);
    }

    public void DisposeUnusedAssets()
    {
        assetRegistry.DelayDisposals = false;
        assetRegistry.DelayDisposals = true;
    }

    private void HandleResize()
    {
        var fb = zzContainer.Framebuffer;
        LogicalScreen = Rect.FromMinMax(
            Vector2.Zero,
            new Vector2(fb.Width, fb.Height));

        var size = LogicalScreen.Size;
        graphicsDevice.UpdateBuffer(ProjectionBuffer, 0, ref size);
    }

    public ITagContainer AddTag<TTag>(TTag tag) where TTag : class => tagContainer.AddTag(tag);
    public TTag GetTag<TTag>() where TTag : class => tagContainer.GetTag<TTag>();
    public IEnumerable<TTag> GetTags<TTag>() where TTag : class => tagContainer.GetTags<TTag>();
    public bool HasTag<TTag>() where TTag : class => tagContainer.HasTag<TTag>();
    public bool RemoveTag<TTag>(bool dispose = true) where TTag : class => tagContainer.RemoveTag<TTag>(dispose);
    public bool TryGetTag<TTag>(out TTag tag) where TTag : class => tagContainer.TryGetTag(out tag);
}
