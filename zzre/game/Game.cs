using System;
using System.Collections.Generic;
using System.Linq;
using DefaultEcs.System;
using Serilog;
using Veldrid;
using zzio;
using zzio.scn;
using zzio.vfs;
using zzre.materials;
using zzre.rendering;

namespace zzre.game;

public abstract class Game : BaseDisposable, ITagContainer
{
    protected readonly ITagContainer tagContainer;
    protected readonly IZanzarahContainer zzContainer;
    protected readonly AssetLocalRegistry assetRegistry;
    protected readonly ILogger logger;
    protected readonly Remotery profiler;
    protected readonly GameTime time;
    protected readonly DefaultEcs.World ecsWorld;
    protected readonly Camera camera;
    protected readonly UI ui;
    protected readonly OnceAction onceUpdate = new();
    protected readonly systems.SyncedLocation syncedLocation;
    protected ISystem<float> updateSystems = null!; // TODO: Replace static systems list
    protected ISystem<CommandList> renderSystems = null!;
    private RgbaFloat clearColor = RgbaFloat.Black;
    private AssetRegistryStats assetStatsBeforeLoading;
    private float timeBeforeLoading;

    public DefaultEcs.Entity PlayerEntity => // Placeholder during transition
        ecsWorld.GetEntities().With<components.PlayerPuppet>().AsEnumerable().First();
    public IResource SceneResource { get; private set; } = null!;

    public bool IsUpdateEnabled
    {
        get => updateSystems.IsEnabled;
        set => updateSystems.IsEnabled = value;
    }

    public Game(ITagContainer diContainer, Savegame savegame)
    {
        tagContainer = new ExtendedTagContainer(diContainer);
        zzContainer = GetTag<IZanzarahContainer>();
        zzContainer.OnResize += HandleResize;
        logger = diContainer.GetLoggerFor<Game>();
        profiler = GetTag<Remotery>();
        time = GetTag<GameTime>();
        ui = GetTag<UI>();

        AddTag(this);
        AddTag(savegame);
        AddTag<IAssetRegistry>(assetRegistry = new AssetLocalRegistry("Game", tagContainer));
        AddTag(ecsWorld = new DefaultEcs.World());
        AddTag(new LocationBuffer(GetTag<GraphicsDevice>(), 4096));
        AddTag(new ModelInstanceBuffer(diContainer, 512)); // TODO: ModelRenderer should use central ModelInstanceBuffer
        AddTag(new EffectMesh(this, 4096, 8192));
        AddTag(camera = new Camera(this));
        if (TryGetTag(out tools.AssetRegistryList assetRegistryList))
            assetRegistryList.Register(GetType().Name, assetRegistry);

        syncedLocation = new systems.SyncedLocation(this);

        ecsWorld.SetMaxCapacity<Scene>(1);
        ecsWorld.SetMaxCapacity<components.SoundListener>(1);
        ecsWorld.Set(new Location()); // world location
        ecsWorld.Subscribe<messages.SpawnSample>(diContainer.GetTag<UI>().Publish); // make sound a bit easier on us
        ecsWorld.Subscribe<messages.SceneLoaded>(DisposeUnusedAssets);
        AssetRegistry.SubscribeAt(ecsWorld);
        assetRegistry.DelayDisposals = true;
    }

    protected override void DisposeManaged()
    {
        assetRegistry.DelayDisposals = false;
        tagContainer.RemoveTag<IAssetRegistry>(dispose: false); // remove all entities first, then destroy registry
        updateSystems.Dispose();
        renderSystems.Dispose();
        tagContainer.Dispose();
        assetRegistry.Dispose();
        zzContainer.OnResize -= HandleResize;
    }

    public void Publish<T>() => ecsWorld.Publish(default(T));
    public void Publish<T>(in T message) => ecsWorld.Publish(message);

    private void HandleResize()
    {
        var fb = zzContainer.Framebuffer;
        camera.Aspect = fb.Width / (float)fb.Height;
    }

    public void Update()
    {
        using var _ = profiler.SampleCPU("Game.Update");
        assetRegistry.ApplyAssets();
        onceUpdate.Invoke();
        updateSystems.Update(time.Delta);
    }

    public void Render(CommandList cl)
    {
        using var _ = profiler.SampleCPU("Game.Render");
        assetRegistry.ApplyAssets();
        camera.Update(cl);
        syncedLocation.Update(cl);
        cl.ClearColorTarget(0, clearColor);
        renderSystems.Update(cl);
    }

    public void LoadScene(string sceneName, Func<Trigger> findEntryTrigger)
    {
        logger.Information("Load " + sceneName);
        timeBeforeLoading = time.TotalElapsed;
        assetStatsBeforeLoading = assetRegistry.Stats;
        ecsWorld.Publish(new messages.SceneChanging(sceneName));
        ecsWorld.Publish(messages.LockPlayerControl.Unlock); // otherwise the timed entry locking will be ignored

        var resourcePool = GetTag<IResourcePool>();
        SceneResource = resourcePool.FindFile($"resources/worlds/{sceneName}.scn") ??
            throw new System.IO.FileNotFoundException($"Could not find scene: {sceneName}"); ;
        using var sceneStream = SceneResource.OpenContent() ?? throw new System.IO.FileNotFoundException($"Could not open scene: {sceneName}");
        var scene = new Scene();
        scene.Read(sceneStream);
        ecsWorld.Set(scene);
        clearColor = (scene.misc.clearColor.ToFColor() with { a = 1f }).ToVeldrid();

        ecsWorld.Publish(new messages.SceneLoaded(scene, GetTag<Savegame>()));
        ecsWorld.Publish(new messages.PlayerEntered(findEntryTrigger()));
    }

    private void DisposeUnusedAssets(in messages.SceneLoaded _)
    {
        var assetStatsBeforeRemoving = assetRegistry.Stats;
        assetRegistry.DelayDisposals = false;
        assetRegistry.DelayDisposals = true;
        ui.DisposeUnusedAssets();
        var assetStatsAfterLoading = assetRegistry.Stats;
        var removalDiff = assetStatsAfterLoading - assetStatsBeforeRemoving;
        var totalDiff = assetStatsAfterLoading - assetStatsBeforeLoading;
        var asyncCreated = totalDiff.Created - totalDiff.Loaded;
        float timeAfterLoading = time.TotalElapsed - timeBeforeLoading;

        logger.Debug("Asset stats: New-{New} Async-{Async} Disposed-{Disposed} in {Time}sec, Total-{Total}",
            totalDiff.Created, asyncCreated, removalDiff.Removed, timeAfterLoading, assetStatsAfterLoading.Total);
    }

    public ITagContainer AddTag<TTag>(TTag tag) where TTag : class => tagContainer.AddTag(tag);
    public TTag GetTag<TTag>() where TTag : class => tagContainer.GetTag<TTag>();
    public IEnumerable<TTag> GetTags<TTag>() where TTag : class => tagContainer.GetTags<TTag>();
    public bool HasTag<TTag>() where TTag : class => tagContainer.HasTag<TTag>();
    public bool RemoveTag<TTag>(bool dispose = true) where TTag : class => tagContainer.RemoveTag<TTag>(dispose);
    public bool TryGetTag<TTag>(out TTag tag) where TTag : class => tagContainer.TryGetTag(out tag);
}
