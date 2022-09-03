﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using DefaultEcs.Resource;
using DefaultEcs.System;
using zzio;
using zzio.scn;
using zzre.rendering;

namespace zzre.game.systems
{
    public class ModelLoader : BaseDisposable, ISystem<float>
    {
        private readonly ITagContainer diContainer;
        private readonly DefaultEcs.World ecsWorld;
        private readonly IDisposable sceneChangingSubscription;
        private readonly IDisposable sceneLoadSubscription;

        public ModelLoader(ITagContainer diContainer)
        {
            this.diContainer = diContainer;
            ecsWorld = diContainer.GetTag<DefaultEcs.World>();
            sceneChangingSubscription = ecsWorld.Subscribe<messages.SceneChanging>(HandleSceneChanging);
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

        private void HandleSceneChanging(in messages.SceneChanging _) => ecsWorld
            .GetEntities()
            .With<components.RenderOrder>()
            .DisposeAll();

        private void HandleSceneLoaded(in messages.SceneLoaded message)
        {
            if (!IsEnabled)
                return;

            var scene = message.Scene;
            int modelCount = scene.models.Length + scene.foModels.Length;
            if (!diContainer.TryGetTag<ModelInstanceBuffer>(out var prevBuffer) || prevBuffer.TotalCount < modelCount)
            {
                prevBuffer?.Dispose();
                diContainer.RemoveTag<ModelInstanceBuffer>();
                diContainer.AddTag(new ModelInstanceBuffer(diContainer, scene.models.Length + scene.foModels.Length, dynamic: true));
            }
            else
                prevBuffer.Clear();

            float plantWiggleDelay = 0f;
            var behaviors = scene.behaviors.ToDictionary(b => b.modelId, b => b.type);
            foreach (var model in scene.models)
            {
                var entity = ecsWorld.CreateEntity();
                entity.Set(new Location()
                {
                    Parent = ecsWorld.Get<Location>(),
                    LocalPosition = model.pos,
                    LocalRotation = model.rot.ToZZRotation()
                });

                entity.Set(ManagedResource<ClumpBuffers>.Create(
                    new resources.ClumpInfo(resources.ClumpType.Model, model.filename + ".dff")));

                LoadMaterialsFor(entity, FOModelRenderType.Solid, model.color, model.surfaceProps);
                SetCollider(entity);
                SetPlantWiggle(entity, model.wiggleAmpl, plantWiggleDelay);
                if (behaviors.TryGetValue(model.idx, out var behaviour))
                    SetBehaviour(entity, behaviour, model.idx);
                if (entity.Has<components.Collidable>())
                    SetIntersectionable(entity);

                plantWiggleDelay++;
            }

            foreach (var foModel in scene.foModels)
            {
                // TODO: Add FOModel filter by render detail

                var entity = ecsWorld.CreateEntity();
                entity.Set(new Location()
                {
                    Parent = ecsWorld.Get<Location>(),
                    LocalPosition = foModel.pos,
                    LocalRotation = foModel.rot.ToZZRotation()
                });

                entity.Set(ManagedResource<ClumpBuffers>.Create(
                    new resources.ClumpInfo(resources.ClumpType.Model, foModel.filename + ".dff")));

                LoadMaterialsFor(entity, foModel.renderType, foModel.color, foModel.surfaceProps);
                SetCollider(entity);
                SetPlantWiggle(entity, foModel.wiggleAmpl, plantWiggleDelay);

                // TODO: Add FOModel distance fading

                plantWiggleDelay++;
            }
        }

        // Used by e.g. NPCTrigger
        internal static void LoadMaterialsFor(DefaultEcs.Entity entity, FOModelRenderType renderType, IColor color, SurfaceProperties surfaceProps)
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
            entity.Set(ManagedResource<materials.BaseModelInstancedMaterial>.Create(rwMaterials
                .Select(rwMaterial => new resources.ClumpMaterialInfo(renderType, rwMaterial))
                .ToArray()));
        }

        private static void SetCollider(DefaultEcs.Entity entity)
        {
            var clumpBuffers = entity.Get<ClumpBuffers>();
            var halfSize = clumpBuffers.Bounds.HalfSize;
            var radius = Math.Max(halfSize.Y, halfSize.Z); // yes, only y and z are relevant
            entity.Set(new Sphere(0f, 0f, 0f, radius));
        }

        private static void SetIntersectionable(DefaultEcs.Entity entity)
        {
            var clumpBuffers = entity.Get<ClumpBuffers>();
            var location = entity.Get<Location>();
            entity.Set(GeometryCollider.CreateFor(clumpBuffers.RWGeometry, location));
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
            FOModelRenderType.EarlySolid => components.RenderOrder.EarlySolid,
            FOModelRenderType.Solid => components.RenderOrder.Solid,
            FOModelRenderType.LateSolid => components.RenderOrder.LateSolid,
            FOModelRenderType.EarlyAdditive => components.RenderOrder.EarlyAdditive,
            FOModelRenderType.Additive => components.RenderOrder.Additive,
            FOModelRenderType.LateAdditive => components.RenderOrder.LateAdditive,
            FOModelRenderType.EnvMap32 => components.RenderOrder.EnvMap,
            FOModelRenderType.EnvMap64 => components.RenderOrder.EnvMap,
            FOModelRenderType.EnvMap96 => components.RenderOrder.EnvMap,
            FOModelRenderType.EnvMap128 => components.RenderOrder.EnvMap,
            FOModelRenderType.EnvMap196 => components.RenderOrder.EnvMap,
            FOModelRenderType.EnvMap255 => components.RenderOrder.EnvMap,

            _ => throw new NotSupportedException($"Unsupported FOModelRenderType: {type}")
        };

        private static readonly IReadOnlyList<Vector2> WiggleAmplitudes = new[]
        {
            new Vector2(0.016f, 0.0004f),
            new Vector2(0.012f, 0.016f),
            new Vector2(0.024f, 0.024f),
            new Vector2(0.036f, 0.036f)
        };

        private static void SetBehaviour(DefaultEcs.Entity entity, BehaviourType behaviour, uint modelId)
        {
            switch (behaviour)
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

                case BehaviourType.River2: entity.Set(new components.behaviour.UVShift() { Shift = 1f }); break;
                case BehaviourType.River3: entity.Set(new components.behaviour.UVShift() { Shift = 2f }); break;
                case BehaviourType.River4: entity.Set(new components.behaviour.UVShift() { Shift = 3f }); break;
                case BehaviourType.River5: entity.Set(new components.behaviour.UVShift() { Shift = 0.01f }); break;
                case BehaviourType.River6: entity.Set(new components.behaviour.UVShift() { Shift = 0.02f }); break;
                case BehaviourType.River7: entity.Set(new components.behaviour.UVShift() { Shift = 0.04f }); break;
                case BehaviourType.River8: entity.Set(new components.behaviour.UVShift() { Shift = 0.06f }); break;
                case BehaviourType.SkyMovement: entity.Set(new components.behaviour.UVShift() { Shift = 0.03f }); break;

                // parameters and names are correct because original, don't mind the the inconsistencies
                case BehaviourType.DoorYellow: entity.Set(new components.behaviour.Door(isRight: false, speed: 190f, keyItemId: StdItemId.GreenBoneKey)); break;
                case BehaviourType.DoorRed: entity.Set(new components.behaviour.Door(isRight: false, speed: 190f, keyItemId: StdItemId.RedBoneKey)); break;
                case BehaviourType.DoorBlue: entity.Set(new components.behaviour.Door(isRight: false, speed: 190f, keyItemId: StdItemId.BlueBoneKey)); break;
                case BehaviourType.DoorSilver: entity.Set(new components.behaviour.Door(isRight: true, speed: 190f, keyItemId: StdItemId.HeavyIronKey)); break;
                case BehaviourType.DoorGold: entity.Set(new components.behaviour.Door(isRight: true, speed: 190f, keyItemId: StdItemId.CatacombsKey)); break;
                case BehaviourType.DoorIron: entity.Set(new components.behaviour.Door(isRight: true, speed: 190f, keyItemId: StdItemId.DwarvFactoryKey)); break;
                case BehaviourType.DoorBronze: entity.Set(new components.behaviour.Door(isRight: true, speed: 190f, keyItemId: StdItemId.RufusKey)); break;
                case BehaviourType.DoorCopper: entity.Set(new components.behaviour.Door(isRight: true, speed: 190f, keyItemId: StdItemId.KeyOfPixieGuard)); break;
                case BehaviourType.DoorPlating: entity.Set(new components.behaviour.Door(isRight: true, speed: 190f, keyItemId: null)); break;
                case BehaviourType.DoorGlass: entity.Set(new components.behaviour.Door(isRight: true, speed: 190f, keyItemId: StdItemId.TownsHallKey)); break;
                case BehaviourType.LockedMetalDoor: entity.Set(new components.behaviour.Door(isRight: false, speed: 190f, keyItemId: null)); break;
                case BehaviourType.LockedWoodenDoor: entity.Set(new components.behaviour.Door(isRight: true, speed: 190f, keyItemId: null)); break;
                case BehaviourType.SimpleDoorLeft: entity.Set(new components.behaviour.Door(isRight: true, speed: 190f, keyItemId: null)); break;
                case BehaviourType.SimpleDoorRight: entity.Set(new components.behaviour.Door(isRight: true, speed: -190f, keyItemId: null)); break;
                case BehaviourType.MetalDoorLeft: entity.Set(new components.behaviour.Door(isRight: false, speed: -190f, keyItemId: null)); break;

                case BehaviourType.CityDoorUp: entity.Set(new components.behaviour.CityDoor(speed: 1.5f, keyItemId: null)); break;
                case BehaviourType.CityDoorDown: entity.Set(new components.behaviour.CityDoor(speed: -1.5f, keyItemId: null)); break;
                case BehaviourType.CityDoorLock: entity.Set(new components.behaviour.CityDoor(speed: 1.5f, keyItemId: StdItemId.HeavyIronKey)); break;

                case BehaviourType.Collectable:
                    entity.Set(new components.behaviour.Collectable() { IsDynamic = false, ModelId = modelId });
                    // TODO: Add shadow to collectable
                    break;
                case BehaviourType.Collectable_EFF0:
                case BehaviourType.Collectable_EFF1:
                    entity.Set(new components.behaviour.Collectable() { IsDynamic = false, ModelId = modelId });
                    // TODO: Add 4004 effect to collectable_eff0/1
                    break;
                case BehaviourType.Collectable_Physics:
                    entity.Set(new components.behaviour.Collectable() { IsDynamic = true, ModelId = modelId });
                    entity.Set<components.behaviour.CollectablePhysics>();
                    break;

                case BehaviourType.MagicBridgeStatic: entity.Set<components.Collidable>(); break;
                case BehaviourType.MagicBridge0:
                    var speed = 15f + MathF.Abs(GlobalRandom.Get.NextFloat()) * 10f;
                    entity.Set(new components.behaviour.Rotate(Vector3.UnitX, speed));
                    entity.Set<components.Collidable>();
                    break;
                case BehaviourType.MagicBridge1:
                    entity.Set(new components.behaviour.MagicBridge(-2.2f));
                    entity.Set<components.Collidable>();
                    break;
                case BehaviourType.MagicBridge2:
                    entity.Set(new components.behaviour.MagicBridge(2.2f));
                    entity.Set<components.Collidable>();
                    break;

                default: Console.WriteLine($"Warning: unsupported behaviour type {behaviour}"); break;
            }
        }
    }
}
