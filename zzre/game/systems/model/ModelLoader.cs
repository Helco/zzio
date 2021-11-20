using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
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

            float plantWiggleDelay = 0f;

            var behaviors = scene.behaviors.ToDictionary(b => b.modelId, b => b.type);

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
                SetCollider(entity);
                SetPlantWiggle(entity, model.wiggleAmpl, plantWiggleDelay);
                if (behaviors.TryGetValue(model.idx, out var behaviour))
                    SetBehaviour(entity, behaviour);

                plantWiggleDelay++;
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
                SetCollider(entity);
                SetPlantWiggle(entity, foModel.wiggleAmpl, plantWiggleDelay);

                // TODO: Add FOModel distance fading

                plantWiggleDelay++;
            }
        }

        private static void LoadMaterialsFor(DefaultEcs.Entity entity, FOModelRenderType renderType, IColor color, SurfaceProperties surfaceProps)
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

        private static void SetCollider(DefaultEcs.Entity entity)
        {
            var clumpBuffers = entity.Get<ClumpBuffers>();
            var halfSize = clumpBuffers.Bounds.HalfSize;
            var radius = Math.Max(halfSize.Y, halfSize.Z); // yes, only y and z are relevant
            entity.Set(new Sphere(0f, 0f, 0f, radius));
        }

        private static void SetPlantWiggle(DefaultEcs.Entity entity, int wiggleAmplitude, float delay)
        {
            wiggleAmplitude--;
            if (wiggleAmplitude < 0 || wiggleAmplitude >= WiggleAmplitudes.Count)
                return;
            entity.Set(new components.PlantWiggle
            {
                Amplitude = WiggleAmplitudes[wiggleAmplitude],
                Delay = delay
            });
        }

        private static components.RenderOrder RenderOrderFromRenderType(FOModelRenderType type) => type switch
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

        private static readonly IReadOnlyList<Vector2> WiggleAmplitudes = new[]
        {
            new Vector2(0.016f, 0.0004f),
            new Vector2(0.012f, 0.016f),
            new Vector2(0.024f, 0.024f),
            new Vector2(0.036f, 0.036f)
        };

        private static void SetBehaviour(DefaultEcs.Entity entity, BehaviourType behaviour)
        {
            switch(behaviour)
            {
                case BehaviourType.Swing: entity.Set<components.behaviour.Swing>(); break;
                case BehaviourType.Lock: entity.Set<components.behaviour.Lock>(); break;

                // TODO: Check rotation direction for Y and Z
                case BehaviourType.XRotate1: entity.Set(new components.behaviour.Rotate(Vector3.UnitX, -5f)); break;
                case BehaviourType.XRotate2: entity.Set(new components.behaviour.Rotate(Vector3.UnitX, -25f)); break;
                case BehaviourType.YRotate1: entity.Set(new components.behaviour.Rotate(Vector3.UnitX, 2f)); break;
                case BehaviourType.YRotate2: entity.Set(new components.behaviour.Rotate(Vector3.UnitX, 4.5f)); break;
                case BehaviourType.ZRotate1: entity.Set(new components.behaviour.Rotate(Vector3.UnitZ, -5f)); break;
                case BehaviourType.ZRotate2: entity.Set(new components.behaviour.Rotate(Vector3.UnitZ, -25f)); break;

                default: Console.WriteLine($"Warning: unsupported behaviour type {behaviour}"); break;
            }
        }
    }
}
