﻿using System.Numerics;
using Veldrid;
using zzio;
using zzio.db;
using zzre.rendering;

namespace zzre.game;

public sealed class DuelGame : Game
{
    public DuelGame(ITagContainer diContainer, messages.StartDuel message) : base(diContainer, message.Savegame)
    {
        AddTag(this);

        // create it now for extra priority in the scene loading events
        var worldRenderer = new systems.WorldRendererSystem(this);
        var fogModifier = new systems.FogModifier(this);

        var updateSystems = new systems.RecordingSequentialSystem<float>(this);
        this.updateSystems = updateSystems;
        updateSystems.Add(
            new systems.PlayerControls(this),

            // Cameras
            new systems.FlyCamera(this),
            new systems.DuelCamera(this) { IsEnabled = true },

            // Models and actors
            new systems.ModelLoader(this),
            new systems.BackdropLoader(this),
            new systems.PlantWiggle(this),
            new systems.DistanceAlphaFade(this),
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

            // Fairies
            new systems.FairyPhysics(this),
            new systems.FairyAnimation(this),
            new systems.FairyActivation(this),
            new systems.FairyVisibilityByDistance(this),
            new systems.FairyVisibilityByCamera(this),

            new systems.TriggerActivation(this),
            new systems.SceneSamples(this),
            new systems.AmbientSounds(this),

            new systems.Reaper(this),
            new systems.ParentReaper(this));
        updateSystems.Add(new systems.PauseDuring(this, updateSystems.Systems));

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

        LoadScene($"sd_{message.SceneId:D4}");
        camera.Location.LocalPosition = -ecsWorld.Get<rendering.WorldMesh>().Origin;

        var playerEntity = CreateParticipant(message.OverworldPlayer, isPlayer: true);
        foreach (var enemy in message.OverworldEnemies)
            CreateParticipant(enemy, isPlayer: false);

        playerEntity.Set<components.SoundListener>();
        playerEntity.Set(components.DuelCameraMode.ZoomIn);
        ecsWorld.Set(new components.PlayerEntity(playerEntity));

        ecsWorld.Publish(new messages.SwitchFairy(playerEntity));
    }

    private DefaultEcs.Entity CreateParticipant(DefaultEcs.Entity overworldEntity, bool isPlayer)
    {
        var inventory = overworldEntity.Get<Inventory>();
        var duelEntity = ecsWorld.CreateEntity();
        duelEntity.Set(new components.DuelParticipant(overworldEntity));
        duelEntity.Set(new Location());
        duelEntity.Set(inventory);

        ref var participant = ref duelEntity.Get<components.DuelParticipant>();
        for (int i = 0; i < Inventory.FairySlotCount; i++)
        {
            var invFairy = inventory.GetFairyAtSlot(i);
            if (invFairy is null || invFairy.currentMHP == 0)
                continue;
            var fairy = CreateFairyFor(duelEntity, invFairy);
            if (!isPlayer)
                fairy.Set<components.FairyDistanceVisibility>();
            participant.Fairies[i] = fairy;
        }

        return duelEntity;
    }

    private DefaultEcs.Entity CreateFairyFor(DefaultEcs.Entity participant, InventoryFairy invFairy)
    {
        var db = GetTag<MappedDB>();
        var dbRow = db.GetFairy(invFairy.dbUID);
        var fairy = ecsWorld.CreateEntity();
        fairy.Set(new components.Parent(participant));
        fairy.Set(new Location());
        fairy.Set(invFairy);
        fairy.Set(dbRow);
        fairy.Set(components.FindActorFloorCollisions.Default);
        fairy.Set(components.ActorLighting.Default);
        fairy.Set<components.Velocity>();
        fairy.Set<components.PuppetActorMovement>();
        fairy.Set(new components.FairyAnimation()
        {
            TargetDirection = Vector3.UnitX, // does not usually affect overworld fairies
            Current = AnimationType.PixieFlounder // something not used by fairies
        });
        ecsWorld.Publish(new messages.LoadActor(
            AsEntity: fairy,
            ActorName: dbRow.Mesh,
            AssetLoadPriority.Synchronous));

        var actorParts = fairy.Get<components.ActorParts>();
        actorParts.Body.Get<Location>().Parent = fairy.Get<Location>();
        if (actorParts.Wings!.Value.TryGet<Skeleton>(out var wingsSkeleton))
            wingsSkeleton.JumpToAnimation(actorParts.Wings.Value.Get<components.AnimationPool>()[AnimationType.Idle0]);
        actorParts.Body.Set(components.Visibility.Invisible);
        actorParts.Wings?.Set(components.Visibility.Invisible);

        var colliderSize = actorParts.Body.Get<ClumpMesh>().BoundingBox.HalfSize.Y;
        fairy.Set(new Sphere(Vector3.Zero, colliderSize));

        fairy.Disable();
        return fairy;
    }
}
