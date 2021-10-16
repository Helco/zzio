﻿using System;
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

        public Game(ITagContainer diContainer, string sceneName, int entryId)
        {
            tagContainer = new TagContainer().FallbackTo(diContainer);
            zzContainer = GetTag<IZanzarahContainer>();
            zzContainer.OnResize += HandleResize;
            time = GetTag<GameTime>();

            AddTag(ecsWorld = new DefaultEcs.World());
            AddTag(new LocationBuffer(GetTag<GraphicsDevice>(), 4096));
            AddTag(camera = new Camera(this));
            AddTag(scene = LoadScene(sceneName));
            AddTag(worldBuffers = LoadWorldBuffers());
            AddTag(worldRenderer = new WorldRenderer(this));
            worldRenderer.WorldBuffers = worldBuffers;

            AddTag(new resources.Clump(this));
            AddTag(new resources.Actor(this));
            AddTag(new resources.SkeletalAnimation(this));

            var flyCameraSystem = new systems.FlyCamera(this);
            flyCameraSystem.IsEnabled = true;
            updateSystems = new SequentialSystem<float>(
                new systems.Animal(this),
                new systems.AdvanceAnimation(this),
                flyCameraSystem);

            renderSystems = new SequentialSystem<CommandList>(
                new systems.SyncedLocation(this),
                new systems.ActorRenderer(this));

            var worldLocation = new Location();
            ecsWorld.Set(worldLocation);
            camera.Location.Parent = worldLocation;
            camera.Location.LocalPosition = -worldBuffers.Origin;

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
            renderSystems.Update(cl);
            worldRenderer.Render(cl);
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