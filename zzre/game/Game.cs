using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using DefaultEcs.System;
using Veldrid;
using zzio;
using zzio.scn;
using zzio.vfs;
using zzre.rendering;

namespace zzre.game
{
    public class Game : BaseDisposable, ITagContainer
    {
        private readonly ITagContainer tagContainer;
        private readonly IZanzarahContainer zzContainer;
        private readonly GameTime time;
        private readonly DefaultEcs.World ecsWorld;
        private readonly Camera camera;
        private readonly Scene scene;
        private readonly WorldBuffers worldBuffers;
        private readonly WorldRenderer worldRenderer;
        private readonly ISystem<float> updateSystems;
        private readonly ISystem<CommandList> renderSystems;
        private readonly systems.SyncedLocation syncedLocation;

        public DefaultEcs.Entity PlayerEntity { get; }
        public IResource SceneResource { get; }

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
            AddTag(scene = LoadScene($"sc_{savegame.sceneId}", out var sceneResource));
            AddTag(worldBuffers = LoadWorldBuffers());
            AddTag(new WorldCollider(worldBuffers.RWWorld));
            AddTag(worldRenderer = new WorldRenderer(this));
            worldRenderer.WorldBuffers = worldBuffers;
            SceneResource = sceneResource;

            AddTag(new resources.Clump(this));
            AddTag(new resources.ClumpMaterial(this));
            AddTag(new resources.Actor(this));
            AddTag(new resources.SkeletalAnimation(this));

            var updateSystems = new systems.RecordingSequentialSystem<float>(this);
            this.updateSystems = updateSystems;
            updateSystems.Add(
                new systems.ModelLoader(this),
                new systems.PlayerControls(this),
                new systems.Animal(this),
                new systems.Butterfly(this),
                new systems.CirclingBird(this),
                new systems.AnimalWaypointAI(this),
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
                new systems.HumanPhysics(this),
                new systems.PlayerPuppet(this),
                new systems.PuppetActorTarget(this),
                new systems.PuppetActorMovement(this),
                new systems.NPC(this),
                new systems.NPCActivator(this),
                new systems.NPCScript(this),
                new systems.NPCMovement(this),
                new systems.NPCIdle(this),
                new systems.NPCLookAtPlayer(this),
                new systems.NPCLookAtTrigger(this),
                new systems.TriggerActivation(this),
                new systems.PlayerTriggers(this),
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
                new systems.Reaper(this),
                new systems.ParentReaper(this));
            updateSystems.Add(new systems.PauseDuringUIScreen(this, updateSystems.Systems));
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
            ecsWorld.Set(worldLocation);
            camera.Location.Parent = worldLocation;
            camera.Location.LocalPosition = -worldBuffers.Origin;

            PlayerEntity = ecsWorld.CreateEntity();
            var playerLocation = new Location();
            playerLocation.Parent = worldLocation;
            playerLocation.LocalPosition = new Vector3(195.02159f, 40.1f, 159.80594f);
            //playerLocation.LocalPosition = scene.triggers.First(t => t.type == TriggerType.Doorway).pos;
            PlayerEntity.Set(playerLocation);
            PlayerEntity.Set(DefaultEcs.Resource.ManagedResource<zzio.ActorExDescription>.Create("chr01"));
            PlayerEntity.Set(components.Visibility.Visible);
            PlayerEntity.Set<components.PlayerControls>();
            PlayerEntity.Set<components.PlayerPuppet>();
            PlayerEntity.Set(components.PhysicParameters.Standard);
            PlayerEntity.Set(new components.NonFairyAnimation(GlobalRandom.Get));
            PlayerEntity.Set<components.PuppetActorMovement>();
            var playerActorParts = PlayerEntity.Get<components.ActorParts>();
            var playerBodyClump = playerActorParts.Body.Get<ClumpBuffers>();
            var playerColliderSize = playerBodyClump.Bounds.Size.Y;
            PlayerEntity.Set(new components.HumanPhysics(playerColliderSize));
            PlayerEntity.Set(new Sphere(Vector3.Zero, playerColliderSize));
            PlayerEntity.Set(new Inventory(this, savegame));

            ecsWorld.Publish(new messages.SceneLoaded(savegame.entryId));

            ecsWorld.GetEntities()
                .With((in Trigger t) => t.idx == 88)
                .AsEnumerable()
                .First().Dispose();
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

        private Scene LoadScene(string sceneName, out IResource sceneResource)
        {
            var resourcePool = GetTag<IResourcePool>();
            sceneResource = resourcePool.FindFile($"resources/worlds/{sceneName}.scn") ??
                throw new System.IO.FileNotFoundException($"Could not find scene: {sceneName}"); ;
            using var sceneStream = sceneResource.OpenContent();
            if (sceneStream == null)
                throw new System.IO.FileNotFoundException($"Could not open scene: {sceneName}");
            var scene = new Scene();
            scene.Read(sceneStream);
            return scene;
        }

        private WorldBuffers LoadWorldBuffers()
        {
            var fullPath = new zzio.FilePath("resources").Combine(scene.misc.worldPath, scene.misc.worldFile + ".bsp");
            return new WorldBuffers(this, fullPath);
        }

        public ITagContainer AddTag<TTag>(TTag tag) where TTag : class => tagContainer.AddTag(tag);
        public TTag GetTag<TTag>() where TTag : class => tagContainer.GetTag<TTag>();
        public IEnumerable<TTag> GetTags<TTag>() where TTag : class => tagContainer.GetTags<TTag>();
        public bool HasTag<TTag>() where TTag : class => tagContainer.HasTag<TTag>();
        public bool RemoveTag<TTag>() where TTag : class => tagContainer.RemoveTag<TTag>();
        public bool TryGetTag<TTag>(out TTag tag) where TTag : class => tagContainer.TryGetTag(out tag);
    }
}