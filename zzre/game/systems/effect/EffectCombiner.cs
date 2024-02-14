using System;
using System.Linq;
using System.Numerics;
using DefaultEcs.Resource;
using DefaultEcs.System;
using Serilog;
using zzio;

namespace zzre.game.systems.effect;

public partial class EffectCombiner : AEntitySetSystem<float>
{
    private readonly ILogger logger;
    private readonly IDisposable sceneChangingSubscription;
    private readonly IDisposable sceneLoadSubscription;
    private readonly IDisposable spawnEffectDisposable;

    public bool AddIndexAsComponent { get; set; }  // used for EffectEditor

    public EffectCombiner(ITagContainer diContainer) : base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, useBuffer: false)
    {
        logger = diContainer.GetLoggerFor<EffectCombiner>();
        sceneChangingSubscription = World.Subscribe<messages.SceneChanging>(HandleSceneChanging);
        sceneLoadSubscription = World.Subscribe<messages.SceneLoaded>(HandleSceneLoaded);
        spawnEffectDisposable = World.Subscribe<messages.SpawnEffectCombiner>(HandleSpawnEffect);
    }

    public override void Dispose()
    {
        base.Dispose();
        sceneLoadSubscription.Dispose();
        sceneChangingSubscription.Dispose();
        spawnEffectDisposable.Dispose();
    }

    private void HandleSceneChanging(in messages.SceneChanging _) => Set.DisposeAll();

    private void HandleSceneLoaded(in messages.SceneLoaded message)
    {
        if (!IsEnabled)
            return;

        foreach (var sceneEffect in message.Scene.effects.Where(e => e.type is zzio.scn.SceneEffectType.Combiner))
        {
            // TODO: Respect min quality level for scene effect combiners
            var entity = World.CreateEntity();
            HandleSpawnEffect(new(
                sceneEffect.effectFile,
                AsEntity: entity,
                Position: sceneEffect.pos));
            entity.Get<Location>().LocalRotation = Quaternion.CreateFromRotationMatrix(
                Matrix4x4.CreateLookAt(Vector3.Zero, sceneEffect.dir, sceneEffect.up));

            entity.Set(sceneEffect.order switch
            {
                zzio.scn.SceneEffectOrder.Early => components.RenderOrder.EarlyEffect,
                zzio.scn.SceneEffectOrder.Solid => components.RenderOrder.Effect,
                zzio.scn.SceneEffectOrder.Late => components.RenderOrder.LateEffect,
                _ => components.RenderOrder.LateEffect
            });
        }
    }

    private void HandleSpawnEffect(in messages.SpawnEffectCombiner msg)
    {
        var entity = msg.AsEntity ?? World.CreateEntity();
        entity.Set(components.Visibility.Visible);
        entity.Set(ManagedResource<zzio.effect.EffectCombiner>.Create(msg.EffectFilename));
        var effect = entity.Get<zzio.effect.EffectCombiner>();
        entity.Set(new components.effect.CombinerPlayback(
            duration: effect.isLooping ? float.PositiveInfinity : effect.Duration,
            depthTest: msg.DepthTest));
        entity.Set(new Location()
        {
            LocalPosition = msg.Position ?? effect.position,
            LocalRotation = Quaternion.CreateFromRotationMatrix(
                Matrix4x4.CreateLookTo(Vector3.Zero, effect.forwards, effect.upwards))
        });
        
        foreach (var (part, index) in effect.parts.Indexed())
        {
            var partEntity = World.CreateEntity();
            if (AddIndexAsComponent)
                partEntity.Set(index);
            partEntity.Set(components.RenderOrder.LateEffect);
            partEntity.SetSameAs<components.Visibility>(entity);
            partEntity.Set(new components.Parent(entity));
            switch(part)
            {
                case zzio.effect.parts.BeamStar beamStar: partEntity.Set(beamStar); break;
                case zzio.effect.parts.ElectricBolt electricBolt: partEntity.Set(electricBolt); break;
                case zzio.effect.parts.Models models: partEntity.Set(models); break;
                case zzio.effect.parts.MovingPlanes movingPlanes: partEntity.Set(movingPlanes); break;
                case zzio.effect.parts.ParticleEmitter emitter: partEntity.Set(emitter); break;
                case zzio.effect.parts.ParticleBeam particleBeam: partEntity.Set(particleBeam); break;
                case zzio.effect.parts.RandomPlanes randomPlanes: partEntity.Set(randomPlanes); break;
                case zzio.effect.parts.Sound sound: partEntity.Set(sound); break;
                case zzio.effect.parts.Sparks sparks: partEntity.Set(sparks); break;
                default:
                    logger.Warning("Unsupported effect combiner part {PartName}", part.Name);
                    break;
            }
        }
    }

    [Update]
    private void Update(
        in DefaultEcs.Entity entity,
        float elapsedTime,
        ref components.effect.CombinerPlayback playback,
        zzio.effect.EffectCombiner effect)
    {
        if (playback.IsFinished)
        {
            entity.Set<components.Dead>();
            return;
        }

        playback.CurTime += elapsedTime;
        if (float.IsInfinity(playback.Duration))
            playback.CurTime %= effect.Duration;
    }
}
