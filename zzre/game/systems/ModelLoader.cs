using System;
using System.Collections.Generic;
using System.Linq;
using DefaultEcs.Resource;
using DefaultEcs.System;
using zzio;
using zzio.primitives;
using zzio.scn;
using zzre.rendering;

namespace zzre.game.systems
{
    public class ModelLoader : BaseDisposable, ISystem<float>
    {
        private readonly ITagContainer diContainer;
        private readonly Scene scene;
        private readonly DefaultEcs.World ecsWorld;
        private readonly IDisposable sceneLoadSubscription;

        public ModelLoader(ITagContainer diContainer)
        {
            this.diContainer = diContainer;
            scene = diContainer.GetTag<Scene>();
            ecsWorld = diContainer.GetTag<DefaultEcs.World>();
            sceneLoadSubscription = ecsWorld.Subscribe<messages.SceneLoaded>(HandleSceneLoaded);
        }

        protected override void DisposeManaged()
        {
            base.DisposeManaged();
            sceneLoadSubscription.Dispose();
        }

        public bool IsEnabled { get; set; } = true;
        
        public void Update(float state)
        {
        }

        private void HandleSceneLoaded(in messages.SceneLoaded message)
        {
            if (!IsEnabled)
                return;

            if (diContainer.HasTag<ModelInstanceBuffer>())
                throw new InvalidOperationException("ModelInstanceBuffer is already created");
            diContainer.AddTag(new ModelInstanceBuffer(diContainer, scene.models.Length + scene.foModels.Length, dynamic: true));

            foreach (var model in scene.models)
            {
                var entity = ecsWorld.CreateEntity();
                var location = new Location();
                location.Parent = ecsWorld.Get<Location>();
                location.LocalPosition = model.pos.ToNumerics();
                location.LocalRotation = model.rot.ToNumericsRotation();
                entity.Set(location);

                entity.Set(ManagedResource<ClumpBuffers>.Create(
                    new resources.ClumpInfo(resources.ClumpType.Model, model.filename + ".dff")));

                LoadMaterialsFor(entity, FOModelRenderType.Solid, model.color, model.surfaceProps);

                // TODO: Add plant model wiggling
                // TODO: Add model colliders
            }

            foreach (var foModel in scene.foModels)
            {
                // TODO: Add FOModel filter by render detail

                var entity = ecsWorld.CreateEntity();
                var location = new Location();
                location.Parent = ecsWorld.Get<Location>();
                location.LocalPosition = foModel.pos.ToNumerics();
                location.LocalRotation = foModel.rot.ToNumericsRotation();
                entity.Set(location);

                entity.Set(ManagedResource<ClumpBuffers>.Create(
                    new resources.ClumpInfo(resources.ClumpType.Model, foModel.filename + ".dff")));

                LoadMaterialsFor(entity, foModel.renderType, foModel.color, foModel.surfaceProps);

                // TODO: Add FOModel distance fading
            }
        }

        private void LoadMaterialsFor(DefaultEcs.Entity entity, FOModelRenderType renderType, IColor color, SurfaceProperties surfaceProps)
        {
            var clumpBuffers = entity.Get<ClumpBuffers>();
            entity.Set(components.Visibility.Visible);
            entity.Set(RenderOrderFromRenderType(renderType));
            entity.Set(renderType);
            entity.Set(new components.ClumpMaterialInfo()
            {
                Color = color,
                SurfaceProperties = surfaceProps
            });
            entity.Set(new List<materials.BaseModelInstancedMaterial>(clumpBuffers.SubMeshes.Count));

            var rwMaterials = clumpBuffers.SubMeshes.Select(sm => sm.Material);
            foreach (var rwMaterial in rwMaterials)
                entity.Set(ManagedResource<materials.BaseModelInstancedMaterial>.Create(
                    new resources.ClumpMaterialInfo(renderType, rwMaterial)));
        }

        private components.RenderOrder RenderOrderFromRenderType(FOModelRenderType type) => type switch
        {
            FOModelRenderType.EarlySolid    => components.RenderOrder.EarlySolid,
            FOModelRenderType.Solid         => components.RenderOrder.Solid,
            FOModelRenderType.LateSolid     => components.RenderOrder.LateSolid,
            FOModelRenderType.EarlyAdditive => components.RenderOrder.EarlyAdditive,
            FOModelRenderType.Additive      => components.RenderOrder.Additive,
            FOModelRenderType.LateAdditive  => components.RenderOrder.LateAdditive,
            FOModelRenderType.EnvMap32      => components.RenderOrder.EnvMap,
            FOModelRenderType.EnvMap64      => components.RenderOrder.EnvMap,
            FOModelRenderType.EnvMap96      => components.RenderOrder.EnvMap,
            FOModelRenderType.EnvMap128     => components.RenderOrder.EnvMap,
            FOModelRenderType.EnvMap196     => components.RenderOrder.EnvMap,
            FOModelRenderType.EnvMap255     => components.RenderOrder.EnvMap,

            _ => throw new NotSupportedException($"Unsupported FOModelRenderType: {type}")
        };
    }
}
