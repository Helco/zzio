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

public class Game : BaseDisposable, ITagContainer
{
    private readonly ITagContainer tagContainer;
    private readonly IZanzarahContainer zzContainer;
    private readonly AssetLocalRegistry assetRegistry;
    private readonly ILogger logger;
    private readonly Remotery profiler;
    private readonly GameTime time;
    private readonly DefaultEcs.World ecsWorld;
    private readonly Camera camera;
    private readonly OnceAction onceUpdate = new();
    private readonly ISystem<float> updateSystems;
    private readonly ISystem<CommandList> renderSystems;
    private readonly systems.SyncedLocation syncedLocation;
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
        profiler = diContainer.GetTag<Remotery>();
        time = GetTag<GameTime>();

        AddTag(this);
        AddTag(savegame);
        AddTag<IAssetRegistry>(assetRegistry = new AssetLocalRegistry("Game", tagContainer));
        AddTag(ecsWorld = new DefaultEcs.World());
        AddTag(new LocationBuffer(GetTag<GraphicsDevice>(), 4096));
        AddTag(new ModelInstanceBuffer(diContainer, 512)); // TODO: ModelRenderer should use central ModelInstanceBuffer
        AddTag(new EffectMesh(this, 4096, 8192));
        diContainer.AddTag(camera = new Camera(this)); // TODO: Remove Camera hack

        AddTag(new resources.Clump(this));
        AddTag(new resources.ClumpMaterial(this));
        AddTag(new resources.Actor(this));
        AddTag(new resources.SkeletalAnimation(this));
        AddTag(new resources.EffectCombiner(this));
        AddTag(new resources.EffectMaterial(this));

        // create it now for extra priority in the scene loading events
        var worldRenderer = new systems.WorldRendererSystem(this);
        var fogModifier = new systems.FogModifier(this);

        var updateSystems = new systems.RecordingSequentialSystem<float>(this);
        this.updateSystems = updateSystems;
        updateSystems.Add(
            new systems.Savegame(this),
            new systems.PlayerSpawner(this),
            new systems.PlayerControls(this),

            // Cameras
            new systems.FlyCamera(this),
            new systems.OverworldCamera(this),
            new systems.TriggerCamera(this),
            new systems.CreatureCamera(this),

            // Models and actors
            new systems.ModelLoader(this),
            new systems.BackdropLoader(this),
            new systems.PlantWiggle(this),
            new systems.BehaviourSwing(this),
            new systems.BehaviourRotate(this),
            new systems.BehaviourUVShift(this),
            new systems.BehaviourDoor(this),
            new systems.BehaviourCityDoor(this),
            new systems.BehaviourCollectablePhysics(this),
            new systems.BehaviourCollectable(this),
            new systems.BehaviourMagicBridge(this),
            new systems.MoveToLocation(this),
            new systems.AdvanceAnimation(this),
            new systems.FindActorFloorCollisions(this),
            new systems.ActorLighting(this),

            // Effects
            fogModifier,
            new systems.effect.LensFlare(this),
            new systems.effect.EffectCombiner(this),
            new systems.effect.MovingPlanes(this),
            new systems.effect.RandomPlanes(this),
            new systems.effect.Emitter(this),
            new systems.effect.ParticleEmitter(this),
            new systems.effect.ModelEmitter(this),
            new systems.effect.BeamStar(this),
            new systems.effect.Sound(this),
            new systems.SceneSamples(this),

            // Animals
            new systems.Animal(this),
            new systems.Butterfly(this),
            new systems.CirclingBird(this),
            new systems.AnimalWaypointAI(this),
            new systems.CollectionFairy(this),

            // Player movement
            new systems.HumanPhysics(this),
            new systems.PlayerPuppet(this),
            new systems.PuppetActorTarget(this),
            new systems.PuppetActorMovement(this),

            // NPC
            new systems.NPC(this),
            new systems.NPCActivator(this),
            new systems.NPCScript(this),
            new systems.NPCMovementByState(this),
            new systems.NPCMovementByDialog(this),
            new systems.NPCIdle(this),
            new systems.NPCLookAtPlayer(this),
            new systems.NPCLookAtTrigger(this),

            new systems.TriggerActivation(this),
            new systems.PlayerTriggers(this),

            // Fairies
            new systems.OverworldFairySpawner(this),
            new systems.FairyHoverOffset(this),
            new systems.FairyHoverBehind(this),
            new systems.FairyKeepLastHover(this),
            new systems.FairyAnimation(this),
            new systems.FairyGlowEffect(this),

            // Dialogs
            new systems.DialogScript(this),
            new systems.DialogDelay(this),
            new systems.DialogFadeOut(this),
            new systems.DialogWaitForSayString(this),
            new systems.DialogTalk(this),
            new systems.DialogLookAt(this),
            new systems.DialogChoice(this),
            new systems.DialogTrading(this),
            new systems.DialogGambling(this),
            new systems.DialogChestPuzzle(this),

            new systems.NonFairyAnimation(this),
            new systems.AmbientSounds(this),

            // Gameflows
            new systems.GotCard(this),
            new systems.Doorway(this),
            new systems.UnlockDoor(this),
            new systems.Teleporter(this),

            new systems.Reaper(this),
            new systems.ParentReaper(this));
        updateSystems.Add(new systems.PauseDuring(this, updateSystems.Systems));
        ecsWorld.Publish(new messages.SetCameraMode(-1, default));

        syncedLocation = new systems.SyncedLocation(this);
        var renderSystems = new systems.RecordingSequentialSystem<CommandList>(this);
        this.renderSystems = renderSystems;
        renderSystems.Add(
            fogModifier,
            new systems.ModelRenderer(this, components.RenderOrder.Backdrop),
            worldRenderer,
            new systems.ModelRenderer(this, components.RenderOrder.World),
            new systems.ActorRenderer(this),
            new systems.ModelRenderer(this, components.RenderOrder.EarlySolid),
            new systems.ModelRenderer(this, components.RenderOrder.EarlyAdditive),
            new systems.effect.EffectRenderer(this, components.RenderOrder.EarlyEffect),
            new systems.effect.EffectModelRenderer(this, components.RenderOrder.EarlyEffect),
            new systems.ModelRenderer(this, components.RenderOrder.Solid),
            new systems.ModelRenderer(this, components.RenderOrder.Additive),
            new systems.ModelRenderer(this, components.RenderOrder.EnvMap),
            new systems.effect.EffectRenderer(this, components.RenderOrder.Effect),
            new systems.effect.EffectModelRenderer(this, components.RenderOrder.Effect),
            new systems.ModelRenderer(this, components.RenderOrder.LateSolid),
            new systems.ModelRenderer(this, components.RenderOrder.LateAdditive),
            new systems.effect.EffectRenderer(this, components.RenderOrder.LateEffect),
            new systems.effect.EffectModelRenderer(this, components.RenderOrder.LateEffect));

        ecsWorld.SetMaxCapacity<Scene>(1);
        ecsWorld.SetMaxCapacity<components.SoundListener>(1);
        ecsWorld.Set(new Location()); // world location
        ecsWorld.Subscribe<messages.SpawnSample>(diContainer.GetTag<UI>().Publish); // make sound a bit easier on us
        ecsWorld.Subscribe<messages.SceneLoaded>(DisposeUnusedAssets);
        AssetRegistry.SubscribeAt(ecsWorld);
        assetRegistry.DelayDisposals = true;

        onceUpdate.Next += () => LoadOverworldScene(savegame.sceneId, () => FindEntryTrigger(savegame.entryId));
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

    public void LoadOverworldScene(int sceneId, Func<Trigger> entryTrigger) =>
        LoadScene($"sc_{sceneId:D4}", entryTrigger);

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

    public Trigger? TryFindTrigger(TriggerType type, int ii1 = -1)
    {
        var triggerEntity = ecsWorld
            .GetEntities()
            .With((in Trigger t) => t.type == type && (ii1 < 0 || t.ii1 == ii1))
            .AsEnumerable()
            .FirstOrDefault();
        return triggerEntity == default
            ? null
            : triggerEntity.Get<Trigger>();
    }

    public Trigger FindEntryTrigger(int targetEntry) => (targetEntry < 0
        ? (TryFindTrigger(TriggerType.SingleplayerStartpoint)
        ?? TryFindTrigger(TriggerType.SavePoint)
        ?? TryFindTrigger(TriggerType.MultiplayerStartpoint))

        : TryFindTrigger(TriggerType.Doorway, targetEntry)
        ?? TryFindTrigger(TriggerType.Elevator, targetEntry)
        ?? TryFindTrigger(TriggerType.RuneTarget, targetEntry))

        ?? throw new System.IO.InvalidDataException($"Scene does not have suitable entry trigger for {targetEntry}");

    public Trigger FindEntryTriggerForRune() =>
        TryFindTrigger(TriggerType.RuneTarget) ??
        TryFindTrigger(TriggerType.SingleplayerStartpoint) ??
        throw new System.IO.InvalidDataException($"Scene does not have suitable entry trigger for rune teleporting");

    private void DisposeUnusedAssets(in messages.SceneLoaded _)
    {
        var assetStatsBeforeRemoving = assetRegistry.Stats;
        assetRegistry.DelayDisposals = false;
        assetRegistry.DelayDisposals = true;
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
