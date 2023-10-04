using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using DefaultEcs.System;
using Veldrid;
using zzio;
using zzio.scn;
using zzio.vfs;
using zzre.rendering;

namespace zzre.game;

public class Game : BaseDisposable, ITagContainer
{
    private readonly ITagContainer tagContainer;
    private readonly IZanzarahContainer zzContainer;
    private readonly GameTime time;
    private readonly DefaultEcs.World ecsWorld;
    private readonly Camera camera;
    private readonly ISystem<float> updateSystems;
    private readonly ISystem<CommandList> renderSystems;
    private readonly systems.SyncedLocation syncedLocation;

    private WorldRenderer worldRenderer = null!;

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
        tagContainer = new TagContainer().FallbackTo(diContainer);
        zzContainer = GetTag<IZanzarahContainer>();
        zzContainer.OnResize += HandleResize;
        time = GetTag<GameTime>();

        AddTag(this);
        AddTag(savegame);
        AddTag(ecsWorld = new DefaultEcs.World());
        AddTag(new LocationBuffer(GetTag<GraphicsDevice>(), 4096));
        AddTag(camera = new Camera(this));

        AddTag(new resources.Clump(this));
        AddTag(new resources.ClumpMaterial(this));
        AddTag(new resources.Actor(this));
        AddTag(new resources.SkeletalAnimation(this));

        ecsWorld.SetMaxCapacity<Scene>(1);
        ecsWorld.SetMaxCapacity<WorldBuffers>(1);
        ecsWorld.SetMaxCapacity<WorldCollider>(1);
        ecsWorld.SetMaxCapacity<WorldRenderer>(1);

        var updateSystems = new systems.RecordingSequentialSystem<float>(this);
        this.updateSystems = updateSystems;
        updateSystems.Add(
            new systems.PlayerSpawner(this),
            new systems.PlayerControls(this),
            new systems.ModelLoader(this),
            new systems.PlantWiggle(this),
            new systems.BehaviourSwing(this),
            new systems.BehaviourRotate(this),
            new systems.BehaviourUVShift(this),
            new systems.BehaviourDoor(this),
            new systems.BehaviourCityDoor(this),
            new systems.BehaviourCollectablePhysics(this),
            new systems.BehaviourCollectable(this),
            new systems.BehaviourMagicBridge(this),
            new systems.AdvanceAnimation(this),
            new systems.Animal(this),
            new systems.Butterfly(this),
            new systems.CirclingBird(this),
            new systems.AnimalWaypointAI(this),
            new systems.HumanPhysics(this),
            new systems.PlayerPuppet(this),
            new systems.PuppetActorTarget(this),
            new systems.PuppetActorMovement(this),
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
            new systems.OverworldFairySpawner(this),
            new systems.FairyHoverBehind(this),
            new systems.FairyAnimation(this),
            new systems.DialogScript(this),
            new systems.DialogDelay(this),
            new systems.DialogFadeOut(this),
            new systems.DialogWaitForSayString(this),
            new systems.DialogTalk(this),
            new systems.DialogLookAt(this),
            new systems.DialogChoice(this),
            new systems.NonFairyAnimation(this),
            new systems.Savegame(this),
            new systems.FlyCamera(this),
            new systems.OverworldCamera(this),
            new systems.TriggerCamera(this),
            new systems.CreatureCamera(this),
            new systems.GotCard(this),
            new systems.Doorway(this),
            new systems.UnlockDoor(this),
            new systems.Reaper(this),
            new systems.ParentReaper(this));
        updateSystems.Add(new systems.PauseDuring(this, updateSystems.Systems));
        ecsWorld.Publish(new messages.SetCameraMode(-1, default));

        syncedLocation = new systems.SyncedLocation(this);
        renderSystems = new SequentialSystem<CommandList>(
            new systems.ActorRenderer(this),
            new systems.ModelRenderer(this, components.RenderOrder.EarlySolid),
            new systems.ModelRenderer(this, components.RenderOrder.EarlyAdditive),
            new systems.ModelRenderer(this, components.RenderOrder.Solid),
            new systems.ModelRenderer(this, components.RenderOrder.Additive),
            new systems.ModelRenderer(this, components.RenderOrder.EnvMap),
            new systems.ModelRenderer(this, components.RenderOrder.LateSolid),
            new systems.ModelRenderer(this, components.RenderOrder.LateAdditive));

        var worldLocation = new Location();
        camera.Location.Parent = worldLocation;
        //camera.Location.LocalPosition = -worldBuffers.Origin;
        ecsWorld.Set(worldLocation);

        LoadScene($"sc_{savegame.sceneId:D4}");
        ecsWorld.Publish(new messages.PlayerEntered(FindEntryTrigger(savegame.entryId)));

        ecsWorld.GetEntities()
            .With((in Trigger t) => t.idx == 88)
            .AsEnumerable()
            .FirstOrDefault().Dispose();
    }

    protected override void DisposeManaged()
    {
        updateSystems.Dispose();
        renderSystems.Dispose();
        tagContainer.Dispose();
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
        updateSystems.Update(time.Delta);
        worldRenderer.UpdateVisibility();
    }

    public void Render(CommandList cl)
    {
        camera.Update(cl);
        syncedLocation.Update(cl);
        worldRenderer.Render(cl);
        renderSystems.Update(cl);
    }

    public void LoadScene(string sceneName)
    {
        if (ecsWorld.Has<WorldBuffers>())
            ecsWorld.Get<WorldBuffers>().Dispose();
        worldRenderer?.Dispose();

        var resourcePool = GetTag<IResourcePool>();
        SceneResource = resourcePool.FindFile($"resources/worlds/{sceneName}.scn") ??
            throw new System.IO.FileNotFoundException($"Could not find scene: {sceneName}"); ;
        using var sceneStream = SceneResource.OpenContent();
        if (sceneStream == null)
            throw new System.IO.FileNotFoundException($"Could not open scene: {sceneName}");
        var scene = new Scene();
        scene.Read(sceneStream);

        var worldPath = new FilePath("resources").Combine(scene.misc.worldPath, scene.misc.worldFile + ".bsp");
        var worldBuffers = new WorldBuffers(this, worldPath);

        ecsWorld.Set(scene);
        ecsWorld.Set(worldBuffers);
        ecsWorld.Set(new WorldCollider(worldBuffers.RWWorld));
        ecsWorld.Set(worldRenderer = new WorldRenderer(this));
        worldRenderer.WorldBuffers = worldBuffers;

        ecsWorld.Publish(new messages.SceneLoaded(scene));
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

    public ITagContainer AddTag<TTag>(TTag tag) where TTag : class => tagContainer.AddTag(tag);
    public TTag GetTag<TTag>() where TTag : class => tagContainer.GetTag<TTag>();
    public IEnumerable<TTag> GetTags<TTag>() where TTag : class => tagContainer.GetTags<TTag>();
    public bool HasTag<TTag>() where TTag : class => tagContainer.HasTag<TTag>();
    public bool RemoveTag<TTag>() where TTag : class => tagContainer.RemoveTag<TTag>();
    public bool TryGetTag<TTag>(out TTag tag) where TTag : class => tagContainer.TryGetTag(out tag);
}