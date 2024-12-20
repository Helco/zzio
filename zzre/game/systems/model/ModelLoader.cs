﻿namespace zzre.game.systems;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using DefaultEcs.System;
using Serilog;
using zzio;
using zzio.scn;
using zzre.materials;
using zzre.rendering;

public sealed class ModelLoader : BaseDisposable, ISystem<float>
{
    private readonly ILogger logger;
    private readonly IAssetRegistry assetRegistry;
    private readonly GameConfigSection gameConfig;
    private readonly DefaultEcs.World ecsWorld;
    private readonly IDisposable sceneChangingSubscription;
    private readonly IDisposable sceneLoadSubscription;
    private readonly IDisposable loadModelSubscription;
    private readonly IDisposable createItemSubscription;
    private readonly IDisposable removeModelSubscription;
    // note: we do not react to GSModRemoveItem, removing the visual model is done by BehaviourCollectable at the *correct* time
    // while removing it at load is handled here

    private readonly Dictionary<uint, DefaultEcs.Entity> entitiesById = [];

    public ModelLoader(ITagContainer diContainer)
    {
        logger = diContainer.GetLoggerFor<ModelLoader>();
        assetRegistry = diContainer.GetTag<IAssetRegistry>();
        gameConfig = diContainer.GetTag<GameConfigSection>();
        ecsWorld = diContainer.GetTag<DefaultEcs.World>();
        sceneChangingSubscription = ecsWorld.Subscribe<messages.SceneChanging>(HandleSceneChanging);
        sceneLoadSubscription = ecsWorld.Subscribe<messages.SceneLoaded>(HandleSceneLoaded);
        loadModelSubscription = ecsWorld.Subscribe<messages.LoadModel>(HandleLoadModel);
        createItemSubscription = ecsWorld.Subscribe<messages.CreateItem>(HandleCreateItem);
        removeModelSubscription = ecsWorld.Subscribe<GSModRemoveModel>(HandleRemoveModel);
    }

    protected override void DisposeManaged()
    {
        base.DisposeManaged();
        sceneLoadSubscription.Dispose();
        sceneChangingSubscription.Dispose();
        loadModelSubscription.Dispose();
        createItemSubscription.Dispose();
        removeModelSubscription.Dispose();
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
        entitiesById.Clear();

        float plantWiggleDelay = 0f;
        var behaviors = message.Scene.behaviors.ToDictionary(b => b.modelId, b => b.type);
        var modelsRemoved = message.GetGameState<GSModRemoveModel>();
        var itemsRemoved = message.GetGameState<GSModRemoveItem>();
        foreach (var model in message.Scene.models)
        {
            if (modelsRemoved.Any(mod => mod.ModelId == model.idx) ||
                itemsRemoved.Any(mod => mod.ModelId == model.idx))
                continue;

            var entity = ecsWorld.CreateEntity();
            entitiesById.Add(model.idx, entity);
            entity.Set(new Location()
            {
                Parent = ecsWorld.Get<Location>(),
                LocalPosition = model.pos,
                LocalRotation = model.rot.ToZZRotation()
            });

            bool hasBehavior = behaviors.TryGetValue(model.idx, out var behaviour);
            var renderType = model.isVisualOnly ? FOModelRenderType.Solid : null as FOModelRenderType?;
            var priority = hasBehavior
                ? AssetLoadPriority.Synchronous
                : AssetLoadPriority.High;
            LoadModel(entity, model.filename, model.color, renderType, priority);

            if (hasBehavior)
            {
                SetBehaviour(entity, behaviour, model.idx);
                SetGeometryCollider(entity);
                if (model.wiggleAmpl > 0)
                {
                    model.wiggleAmpl = 0;
                    logger.Warning("Model {ModelIndex} with a {Behaviour} behaviour also has a plant wiggle set.", model.idx, behaviour);
                }
            }
            SetPlantWiggle(entity, model.wiggleAmpl, plantWiggleDelay);

            plantWiggleDelay++;
        }

        foreach (var foModel in message.Scene.foModels)
        {
            if (foModel.worldDetailLevel > (int)gameConfig.WorldQuality)
                continue;

            var entity = ecsWorld.CreateEntity();
            entity.Set(new Location()
            {
                Parent = ecsWorld.Get<Location>(),
                LocalPosition = foModel.pos,
                LocalRotation = foModel.rot.ToZZRotation()
            });
            SetPlantWiggle(entity, foModel.wiggleAmpl, plantWiggleDelay);

            LoadModel(entity, foModel.filename, foModel.color, foModel.renderType, AssetLoadPriority.Low);
            SetDistanceAlphaFade(entity, foModel);

            plantWiggleDelay++;
        }
    }

    private void HandleLoadModel(in messages.LoadModel msg) =>
        LoadModel(msg.AsEntity, msg.ModelName, msg.Color, msg.RenderType, msg.Priority);

    private unsafe void LoadModel(DefaultEcs.Entity entity, string modelName, IColor color, FOModelRenderType? renderType, AssetLoadPriority priority = AssetLoadPriority.Synchronous)
    {
        ClumpMaterialAsset.MaterialVariant material = renderType switch
        {
            null => new(ModelMaterial.BlendMode.Opaque),
            FOModelRenderType.EarlySolid or FOModelRenderType.LateSolid or FOModelRenderType.Solid =>
                new(ModelMaterial.BlendMode.Alpha),
            FOModelRenderType.EarlyAdditive or FOModelRenderType.Additive =>
                new(ModelMaterial.BlendMode.AdditiveAlpha, HasFog: false),
            FOModelRenderType.LateAdditive =>
                new(ModelMaterial.BlendMode.AdditiveAlpha, HasFog: false, DepthWrite: false),
            FOModelRenderType.EnvMap32 or
            FOModelRenderType.EnvMap64 or
            FOModelRenderType.EnvMap96 or
            FOModelRenderType.EnvMap128 or
            FOModelRenderType.EnvMap196 or
            FOModelRenderType.EnvMap255 =>
                new(ModelMaterial.BlendMode.Alpha, HasEnvMap: true, DepthWrite: false),
            _ => throw new NotSupportedException($"Unsupported render type: {renderType}")
        };

        entity.Set(components.Visibility.Visible);
        entity.Set(RenderOrderFromRenderType(renderType));
        entity.Set(color with { a = AlphaFromRenderType(renderType) });
        entity.Set(ClumpAsset.Info.Model(modelName));
        var handle = assetRegistry.LoadModel(entity, modelName, priority, material, StandardTextureKind.White);
        handle.Inner.Apply(&ApplyModelAfterLoading, entity);
    }

    private static void ApplyModelAfterLoading(AssetHandle handle, ref readonly DefaultEcs.Entity entity)
    {
        if (!entity.IsAlive)
            return;
        if (HasEmptyMesh(entity))
            entity.Dispose(); // I am fine with ignoring empty FOModels
        else
            SetSphereCollider(entity);
    }

    private void HandleCreateItem(in messages.CreateItem msg)
    {
        if (msg.Count < 1)
            return;

        for (int i = 0; i < msg.Count; i++)
        {
            var entity = ecsWorld.CreateEntity();
            entity.Set(new Location()
            {
                Parent = ecsWorld.Get<Location>(),
                LocalPosition = msg.Position,
                LocalRotation = Vector3.UnitX.ToZZRotation()
            });
            SetBehaviour(entity, BehaviourType.CollectablePhysics, uint.MaxValue);
            LoadModel(entity, $"itm{msg.ItemId:D3}", IColor.White, FOModelRenderType.Solid, AssetLoadPriority.Synchronous);
        }
    }

    private static bool HasEmptyMesh(DefaultEcs.Entity entity) =>
         entity.Get<ClumpMesh>().IsEmpty;

    private static void SetSphereCollider(DefaultEcs.Entity entity)
    {
        var clumpMesh = entity.Get<ClumpMesh>();
        var halfSize = clumpMesh.BoundingBox.HalfSize;
        var radius = Math.Max(halfSize.Y, halfSize.Z); // yes, only y and z are relevant
        entity.Set(new Sphere(0f, 0f, 0f, radius));
    }

    private static void SetGeometryCollider(DefaultEcs.Entity entity)
    {
        if (!entity.Has<components.Collidable>())
            return;
        // Only scene models with behaviors can be collidable
        // ClumpMeshes for models with behavior are loaded synchronously
        // therefore we always have the ClumpMesh already at this point
        var clumpMesh = entity.Get<ClumpMesh>();
        var location = entity.Get<Location>();
        entity.Set(GeometryCollider.Create(clumpMesh.Geometry, location));
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

    private static void SetDistanceAlphaFade(DefaultEcs.Entity entity, FOModel foModel)
    {
        var baseAlpha = entity.Get<IColor>().a / 255f;
        entity.Set(new components.DistanceAlphaFade(baseAlpha, foModel.fadeOutMin, foModel.fadeOutMax));
    }

    private static components.RenderOrder RenderOrderFromRenderType(FOModelRenderType? type) => type switch
    {
        null => components.RenderOrder.World,
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

    private static byte AlphaFromRenderType(FOModelRenderType? type) => type switch
    {
        FOModelRenderType.EnvMap32 => 32,
        FOModelRenderType.EnvMap64 => 64,
        FOModelRenderType.EnvMap96 => 96,
        FOModelRenderType.EnvMap128 => 128,
        FOModelRenderType.EnvMap196 => 196,
        FOModelRenderType.EnvMap255 => 255,
        _ => 255
    };

    private static readonly IReadOnlyList<Vector2> WiggleAmplitudes = new[]
    {
        new Vector2(0.016f, 0.0004f),
        new Vector2(0.012f, 0.016f),
        new Vector2(0.024f, 0.024f),
        new Vector2(0.036f, 0.036f)
    };

    private void SetBehaviour(DefaultEcs.Entity entity, BehaviourType behaviour, uint modelId)
    {
        switch (behaviour)
        {
            case BehaviourType.Swing: entity.Set<components.behaviour.Swing>(); break;
            case BehaviourType.Lock: entity.Set(new components.behaviour.Lock(modelId)); break;

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
            case BehaviourType.LockedMetalDoor: entity.Set(new components.behaviour.Door(isRight: false, speed: 190f, keyItemId: null, isLocked: true)); break;
            case BehaviourType.LockedWoodenDoor: entity.Set(new components.behaviour.Door(isRight: true, speed: 190f, keyItemId: null, isLocked: true)); break;
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
            case BehaviourType.CollectableEffect0:
            case BehaviourType.CollectableEffect1:
                entity.Set(new components.behaviour.Collectable() { IsDynamic = false, ModelId = modelId });
                // TODO: Add 4004 effect to collectable_eff0/1
                break;
            case BehaviourType.CollectablePhysics:
                entity.Set(new components.behaviour.Collectable() { IsDynamic = true, ModelId = modelId });
                entity.Set<components.behaviour.CollectablePhysics>();
                break;

            case BehaviourType.MagicBridgeStatic: entity.Set<components.Collidable>(); break;
            case BehaviourType.MagicBridge0:
                var speed = 15f + MathF.Abs(Random.Shared.NextFloat()) * 10f;
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

            default: logger.Warning("Unsupported behaviour type {Behaviour}", behaviour); break;
        }
    }

    private void HandleRemoveModel(in GSModRemoveModel model) => HandleRemove(model.ModelId);
    private void HandleRemove(uint modelId)
    {
        if (entitiesById.TryGetValue(modelId, out var entity))
            entity.Set<components.Dead>();
    }
}
