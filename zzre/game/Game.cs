using System.Collections.Generic;
using System.Numerics;
using DefaultEcs.System;
using Veldrid;
using zzio.scn;
using zzio.utils;
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

        public Game(ITagContainer diContainer, string sceneName, int entryId)
        {
            tagContainer = new TagContainer().FallbackTo(diContainer);
            zzContainer = GetTag<IZanzarahContainer>();
            zzContainer.OnResize += HandleResize;
            time = GetTag<GameTime>();

            AddTag(this);
            AddTag(ecsWorld = new DefaultEcs.World());
            AddTag(new LocationBuffer(GetTag<GraphicsDevice>(), 4096));
            AddTag(camera = new Camera(this));
            AddTag(scene = LoadScene(sceneName));
            AddTag(worldBuffers = LoadWorldBuffers());
            AddTag(new WorldCollider(worldBuffers.RWWorld));
            AddTag(worldRenderer = new WorldRenderer(this));
            worldRenderer.WorldBuffers = worldBuffers;

            AddTag(new resources.Clump(this));
            AddTag(new resources.ClumpMaterial(this));
            AddTag(new resources.Actor(this));
            AddTag(new resources.SkeletalAnimation(this));

            var flyCameraSystem = new systems.FlyCamera(this);
            var owCameraSystem = new systems.OverworldCamera(this);
            owCameraSystem.IsEnabled = true;
            updateSystems = new SequentialSystem<float>(
                new systems.ModelLoader(this),
                new systems.PlayerControls(this),
                new systems.Animal(this),
                new systems.Butterfly(this),
                new systems.CirclingBird(this),
                new systems.AnimalWaypointAI(this),
                new systems.AdvanceAnimation(this),
                new systems.HumanPhysics(this),
                new systems.PlayerPuppet(this),
                new systems.PuppetActorMovement(this),
                new systems.NonFairyAnimation(this),
                flyCameraSystem,
                owCameraSystem);

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
            PlayerEntity.Set(playerLocation);
            PlayerEntity.Set(DefaultEcs.Resource.ManagedResource<zzio.ActorExDescription>.Create("chr01"));
            PlayerEntity.Set(components.Visibility.Visible);
            PlayerEntity.Set<components.PlayerControls>();
            PlayerEntity.Set<components.PlayerPuppet>();
            PlayerEntity.Set(components.PhysicParameters.Standard);
            PlayerEntity.Set(new components.NonFairyAnimation(GlobalRandom.Get));
            var playerActorParts = PlayerEntity.Get<components.ActorParts>();
            playerActorParts.Body.Set<components.PuppetActorMovement>();
            var playerBodyClump = playerActorParts.Body.Get<ClumpBuffers>();
            var playerColliderSize = playerBodyClump.Bounds.Size.Y;
            PlayerEntity.Set(new components.HumanPhysics(playerColliderSize));

            ecsWorld.Publish(new messages.SceneLoaded(entryId));
        }

        protected override void DisposeManaged()
        {
            updateSystems.Dispose();
            renderSystems.Dispose();
            tagContainer.Dispose();
            zzContainer.OnResize -= HandleResize;
        }

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

        private Scene LoadScene(string sceneName)
        {
            var resourcePool = GetTag<IResourcePool>();
            using var sceneStream = resourcePool.FindAndOpen($"resources/worlds/{sceneName}.scn");
            if (sceneStream == null)
                throw new System.IO.FileNotFoundException($"Could not open scene: {sceneName}");
            var scene = new Scene();
            scene.Read(sceneStream);
            return scene;
        }

        private WorldBuffers LoadWorldBuffers()
        {
            var fullPath = new FilePath("resources").Combine(scene.misc.worldPath, scene.misc.worldFile + ".bsp");
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